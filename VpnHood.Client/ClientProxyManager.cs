﻿using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using PacketDotNet;
using VpnHood.Client.Device;
using VpnHood.Common.Logging;
using VpnHood.Tunneling;
using VpnHood.Tunneling.Factory;

namespace VpnHood.Client;

internal class ClientProxyManager : ProxyManager
{
    private readonly IPacketCapture _packetCapture;
        
    // PacketCapture can not protect Ping so PingProxy does not work
    protected override bool IsPingSupported => false; 

    public ClientProxyManager(IPacketCapture packetCapture, ISocketFactory socketFactory, ProxyManagerOptions options)
    : base(new ProtectedSocketFactory(packetCapture, socketFactory), options)
    {
        _packetCapture = packetCapture ?? throw new ArgumentNullException(nameof(packetCapture));
    }

    public override Task OnPacketReceived(IPPacket ipPacket)
    {
        if (VhLogger.IsDiagnoseMode)
            PacketUtil.LogPacket(ipPacket, "Delegating packet to host via proxy.");

        _packetCapture.SendPacketToInbound(ipPacket);
        return Task.FromResult(0);
    }

    private class ProtectedSocketFactory : ISocketFactory
    {
        private readonly IPacketCapture _packetCapture;
        private readonly ISocketFactory _socketFactory;

        public ProtectedSocketFactory(IPacketCapture packetCapture, ISocketFactory socketFactory)
        {
            _packetCapture = packetCapture;
            _socketFactory = socketFactory;
        }

        public TcpClient CreateTcpClient(AddressFamily addressFamily)
        {
            var ret = _socketFactory.CreateTcpClient(addressFamily);
            _packetCapture.ProtectSocket(ret.Client);
            return ret;
        }

        public UdpClient CreateUdpClient(AddressFamily addressFamily)
        {
            var ret = _socketFactory.CreateUdpClient(addressFamily);
            _packetCapture.ProtectSocket(ret.Client);
            return ret;
        }

        public void SetKeepAlive(Socket socket, bool enable)
        {
            _socketFactory.SetKeepAlive(socket, enable);
        }

        public void Config(ISocketFactory socketFactory, TcpClient tcpClient, bool noDelay,
            int? receiveBufferSize, int? sendBufferSize)
        {
            tcpClient.NoDelay = noDelay;
            if (receiveBufferSize != null) tcpClient.ReceiveBufferSize = receiveBufferSize.Value;
            if (sendBufferSize != null) tcpClient.SendBufferSize = sendBufferSize.Value;

        }
    }
}