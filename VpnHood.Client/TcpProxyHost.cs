﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using VpnHood.Client.ConnectorServices;
using VpnHood.Common.Logging;
using VpnHood.Common.Messaging;
using VpnHood.Common.Utils;
using VpnHood.Tunneling;
using VpnHood.Tunneling.Channels;
using VpnHood.Tunneling.ClientStreams;
using VpnHood.Tunneling.Messaging;
using ProtocolType = PacketDotNet.ProtocolType;

namespace VpnHood.Client;

internal class TcpProxyHost : IDisposable
{
    private bool _disposed;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<IPPacket> _ipPackets = new();
    private TcpListener? _tcpListenerIpV4;
    private TcpListener? _tcpListenerIpV6;
    private IPEndPoint? _localEndpointIpV4;
    private IPEndPoint? _localEndpointIpV6;
    private VpnHoodClient Client { get; }
    public IPAddress CatcherAddressIpV4 { get; }
    public IPAddress CatcherAddressIpV6 { get; }

    public TcpProxyHost(VpnHoodClient client, IPAddress catcherAddressIpV4, IPAddress catcherAddressIpV6)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        CatcherAddressIpV4 = catcherAddressIpV4 ?? throw new ArgumentNullException(nameof(catcherAddressIpV4));
        CatcherAddressIpV6 = catcherAddressIpV6 ?? throw new ArgumentNullException(nameof(catcherAddressIpV6));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cancellationTokenSource.Cancel();
        _tcpListenerIpV4?.Stop();
        _tcpListenerIpV6?.Stop();
    }

    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TcpProxyHost));
        using var logScope = VhLogger.Instance.BeginScope($"{VhLogger.FormatType<TcpProxyHost>()}");
        VhLogger.Instance.LogInformation($"Starting {VhLogger.FormatType(this)}...");

        // IpV4
        _tcpListenerIpV4 = new TcpListener(IPAddress.Any, 0);
        _tcpListenerIpV4.Start();
        _localEndpointIpV4 = (IPEndPoint)_tcpListenerIpV4.LocalEndpoint; //it is slow; make sure to cache it
        VhLogger.Instance.LogInformation($"{VhLogger.FormatType(this)} is listening on {VhLogger.Format(_localEndpointIpV4)}");
        _ = AcceptTcpClientLoop(_tcpListenerIpV4);

        // IpV6
        try
        {
            _tcpListenerIpV6 = new TcpListener(IPAddress.IPv6Any, 0);
            _tcpListenerIpV6.Start();
            _localEndpointIpV6 = (IPEndPoint)_tcpListenerIpV6.LocalEndpoint; //it is slow; make sure to cache it
            VhLogger.Instance.LogInformation(
                $"{VhLogger.FormatType(this)} is listening on {VhLogger.Format(_localEndpointIpV6)}");
            _ = AcceptTcpClientLoop(_tcpListenerIpV6);
        }
        catch (Exception ex)
        {
            VhLogger.Instance.LogError(ex, $"Could not create listener on {VhLogger.Format(new IPEndPoint(IPAddress.IPv6Any, 0))}!");
        }
    }

    private async Task AcceptTcpClientLoop(TcpListener tcpListener)
    {
        var cancellationToken = _cancellationTokenSource.Token;
        var localEp = (IPEndPoint)tcpListener.LocalEndpoint;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var tcpClient = await VhUtil.RunTask(tcpListener.AcceptTcpClientAsync(), default, cancellationToken);
                _ = ProcessClient(tcpClient, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            if (ex is not ObjectDisposedException)
                VhLogger.Instance.LogError($"{ex.Message}");
        }
        finally
        {
            VhLogger.Instance.LogInformation($"{VhLogger.FormatType(this)} Listener on {localEp} has been closed.");
        }
    }

    // this method should not be called in multi-thread, the return buffer is shared and will be modified on next call
    public IPPacket[] ProcessOutgoingPacket(IEnumerable<IPPacket> ipPackets)
    {
        if (_localEndpointIpV4 == null)
            throw new InvalidOperationException($"{nameof(_localEndpointIpV4)} has not been initialized! Did you call {nameof(Start)}!");

        _ipPackets.Clear(); // prevent reallocation in this intensive method
        var ret = _ipPackets;

        foreach (var item in ipPackets)
        {
            var ipPacket = item;
            var loopbackAddress = ipPacket.Version == IPVersion.IPv4 ? CatcherAddressIpV4 : CatcherAddressIpV6;
            var localEndPoint = ipPacket.Version == IPVersion.IPv4 ? _localEndpointIpV4 : _localEndpointIpV6;
            if (localEndPoint == null)
                continue;

            try
            {
                if (ipPacket.Protocol != ProtocolType.Tcp)
                    throw new InvalidOperationException($"{typeof(TcpProxyHost)} can not handle {ipPacket.Protocol} packets!");

                // extract tcpPacket
                var tcpPacket = PacketUtil.ExtractTcp(ipPacket);
                if (Equals(ipPacket.DestinationAddress, loopbackAddress))
                {
                    // redirect to inbound
                    var natItem = (NatItemEx?)Client.Nat.Resolve(ipPacket.Version, ipPacket.Protocol, tcpPacket.DestinationPort);
                    if (natItem != null)
                    {
                        ipPacket.SourceAddress = natItem.DestinationAddress;
                        ipPacket.DestinationAddress = natItem.SourceAddress;
                        tcpPacket.SourcePort = natItem.DestinationPort;
                        tcpPacket.DestinationPort = natItem.SourcePort;
                    }
                    else
                    {
                        VhLogger.Instance.LogInformation(GeneralEventId.Nat,
                            $"Could not find incoming destination in NAT! Packet has been dropped. Packet: {PacketUtil.Format(ipPacket)}");
                        ipPacket = PacketUtil.CreateTcpResetReply(ipPacket);
                    }
                }
                // Redirect outbound to the local address
                else
                {
                    var sync = tcpPacket is { Synchronize: true, Acknowledgment: false };
                    var natItem = sync
                        ? Client.Nat.Add(ipPacket, true)
                        : Client.Nat.Get(ipPacket);

                    // could not find the tcp session natItem
                    if (natItem != null)
                    {
                        tcpPacket.SourcePort = natItem.NatId; // 1
                        ipPacket.DestinationAddress = ipPacket.SourceAddress; // 2
                        ipPacket.SourceAddress = loopbackAddress; //3
                        tcpPacket.DestinationPort = (ushort)localEndPoint.Port; //4
                    }
                    else
                    {
                        VhLogger.Instance.LogInformation(GeneralEventId.Nat,
                            $"Could not find outgoing tcp destination in NAT! Packet has been dropped. Packet: {PacketUtil.Format(ipPacket)}");
                        ipPacket = PacketUtil.CreateTcpResetReply(ipPacket);
                    }
                }

                PacketUtil.UpdateIpPacket(ipPacket);
                ret.Add(ipPacket);
            }
            catch (Exception ex)
            {
                VhLogger.Instance.LogError(
                    $"{VhLogger.FormatType(this)}: Error in processing packet! Error: {ex}");
            }
        }

        return ret.ToArray(); //it is shared buffer; too array is necessary
    }

    //private async Task ProcessClient2(TcpClient orgTcpClient, CancellationToken cancellationToken)
    //{
    //    // get original remote from NAT
    //    var orgRemoteEndPoint = (IPEndPoint)orgTcpClient.Client.RemoteEndPoint;
    //    var ipVersion = orgRemoteEndPoint.AddressFamily == AddressFamily.InterNetwork
    //        ? IPVersion.IPv4
    //        : IPVersion.IPv6;
    //    var natItem = (NatItemEx?)Client.Nat.Resolve(ipVersion, ProtocolType.Tcp, (ushort)orgRemoteEndPoint.Port);
    //    if (natItem == null)
    //        throw new Exception(
    //            $"Could not resolve original remote from NAT! RemoteEndPoint: {VhLogger.Format(orgTcpClient.Client.RemoteEndPoint)}");

    //    // create a scope for the logger
    //    using var scope = VhLogger.Instance.BeginScope(
    //        $"LocalPort: {natItem.SourcePort}, RemoteEp: {VhLogger.Format(natItem.DestinationAddress)}:{natItem.DestinationPort}");
    //    VhLogger.Instance.LogTrace(GeneralEventId.StreamChannel, "New TcpProxy Request.");

    //    // check invalid income
    //    var loopbackAddress = ipVersion == IPVersion.IPv4 ? LoopbackAddressIpV4 : LoopbackAddressIpV6;
    //    if (!Equals(orgRemoteEndPoint.Address, loopbackAddress))
    //        throw new Exception("TcpProxy rejected an outbound connection!");

    //}

    private async Task ProcessClient(TcpClient orgTcpClient, CancellationToken cancellationToken)
    {
        if (orgTcpClient is null) throw new ArgumentNullException(nameof(orgTcpClient));
        ConnectorRequestResult<SessionResponseBase>? connectorRequest = null;
        StreamProxyChannel? channel = null;

        try
        {
            // config tcpOrgClient
            Client.SocketFactory.SetKeepAlive(orgTcpClient.Client, true);

            // get original remote from NAT
            var orgRemoteEndPoint = (IPEndPoint)orgTcpClient.Client.RemoteEndPoint;
            var ipVersion = orgRemoteEndPoint.AddressFamily == AddressFamily.InterNetwork
                ? IPVersion.IPv4
                : IPVersion.IPv6;

            var natItem = (NatItemEx?)Client.Nat.Resolve(ipVersion, ProtocolType.Tcp, (ushort)orgRemoteEndPoint.Port)
                          ?? throw new Exception($"Could not resolve original remote from NAT! RemoteEndPoint: {VhLogger.Format(orgTcpClient.Client.RemoteEndPoint)}");

            // create a scope for the logger
            using var scope = VhLogger.Instance.BeginScope(
                $"LocalPort: {natItem.SourcePort}, RemoteEp: {VhLogger.Format(natItem.DestinationAddress)}:{natItem.DestinationPort}");
            VhLogger.Instance.LogTrace(GeneralEventId.TcpProxyChannel, "New TcpProxy Request.");

            // check invalid income
            var loopbackAddress = ipVersion == IPVersion.IPv4 ? CatcherAddressIpV4 : CatcherAddressIpV6;
            if (!Equals(orgRemoteEndPoint.Address, loopbackAddress))
                throw new Exception("TcpProxy rejected an outbound connection!");

            // Check IpFilter
            if (!Client.IsInIpRange(natItem.DestinationAddress))
            {
                await Client.AddPassthruTcpStream(
                    new TcpClientStream(orgTcpClient, orgTcpClient.GetStream()),
                    new IPEndPoint(natItem.DestinationAddress, natItem.DestinationPort),
                    cancellationToken);
                return;
            }

            // Create the Request
            var request = new TcpProxyChannelRequest(
                Client.SessionId,
                Client.SessionKey,
                new IPEndPoint(natItem.DestinationAddress, natItem.DestinationPort),
                VhUtil.GenerateKey(),
                natItem.DestinationPort == 443 ? TunnelUtil.TlsHandshakeLength : -1);

            // read the response
            connectorRequest = await Client.SendRequest<SessionResponseBase>(RequestCode.TcpProxyChannel, request, cancellationToken);
            var tcpProxyClientStream = connectorRequest.TcpClientStream;
            tcpProxyClientStream.TcpClient.ReceiveBufferSize = orgTcpClient.ReceiveBufferSize;
            tcpProxyClientStream.TcpClient.SendBufferSize = orgTcpClient.SendBufferSize;
            Client.SocketFactory.SetKeepAlive(tcpProxyClientStream.TcpClient.Client, true);

            // create a TcpProxyChannel
            VhLogger.Instance.LogTrace(GeneralEventId.TcpProxyChannel,
                $"Adding a channel to session {VhLogger.FormatId(request.SessionId)}...");
            var orgTcpClientStream = new TcpClientStream(orgTcpClient, orgTcpClient.GetStream());

            // Dispose ssl stream and replace it with a HeadCryptor
            await tcpProxyClientStream.Stream.DisposeAsync();
            tcpProxyClientStream.Stream = StreamHeadCryptor.Create(tcpProxyClientStream.TcpClient.GetStream(),
                request.CipherKey, null, request.CipherLength);

            channel = new StreamProxyChannel(orgTcpClientStream, tcpProxyClientStream, TunnelUtil.TcpTimeout);
            Client.Tunnel.AddChannel(channel);
        }
        catch (Exception ex)
        {
            if (channel!=null) await channel.DisposeAsync();
            if (connectorRequest!=null ) await connectorRequest.DisposeAsync();
            orgTcpClient.Dispose();
            VhLogger.Instance.LogError(GeneralEventId.TcpProxyChannel, $"{ex.Message}");
        }
    }
}