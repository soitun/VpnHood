﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.WinDivert;
using VpnHood.Common.Logging;
using VpnHood.Common.Net;

namespace VpnHood.Client.Device.WinDivert;

public class WinDivertPacketCapture : IPacketCapture
{
    private readonly SharpPcap.WinDivert.WinDivertDevice _device;

    private bool _disposed;
    private IpNetwork[]? _includeNetworks;
    private WinDivertHeader? _lastCaptureHeader;

    public WinDivertPacketCapture()
    {
        // initialize devices
        _device = new SharpPcap.WinDivert.WinDivertDevice { Flags = 0 };
        _device.OnPacketArrival += Device_OnPacketArrival;
    }

    public event EventHandler<PacketReceivedEventArgs>? OnPacketReceivedFromInbound;
    public event EventHandler? OnStopped;

    public bool Started => _device.Started;
    public virtual bool CanSendPacketToOutbound => true;

    public virtual bool IsDnsServersSupported => false;

    public virtual IPAddress[]? DnsServers
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public virtual bool CanProtectSocket => false;

    public virtual void ProtectSocket(Socket socket)
    {
        throw new NotSupportedException(
            $"{nameof(ProtectSocket)} is not supported by {GetType().Name}");
    }

    public void SendPacketToInbound(IEnumerable<IPPacket> ipPackets)
    {
        foreach (var ipPacket in ipPackets)
            SendPacket(ipPacket, false);
    }

    public void SendPacketToInbound(IPPacket ipPacket)
    {
        SendPacket(ipPacket, false);
    }

    public void SendPacketToOutbound(IPPacket ipPacket)
    {
        SendPacket(ipPacket, true);
    }

    public void SendPacketToOutbound(IEnumerable<IPPacket> ipPackets)
    {
        foreach (var ipPacket in ipPackets)
            SendPacket(ipPacket, true);
    }

    public IpNetwork[]? IncludeNetworks
    {
        get => _includeNetworks;
        set
        {
            if (Started)
                throw new InvalidOperationException(
                    $"Can't set {nameof(IncludeNetworks)} when {nameof(WinDivertPacketCapture)} is started!");
            _includeNetworks = value;
        }
    }

    private string Ip(IpRange ipRange)
    {
        return ipRange.AddressFamily == AddressFamily.InterNetworkV6 ? "ipv6" : "ip";
    }

    public void StartCapture()
    {
        if (Started)
            throw new InvalidOperationException("_device has been already started!");

        // create include and exclude phrases
        var phraseX = "true";
        if (IncludeNetworks != null)
        {
            var ipRanges = IpNetwork.ToIpRange(IncludeNetworks);
            var phrases = ipRanges.Select(x => x.FirstIpAddress.Equals(x.LastIpAddress)
                ? $"{Ip(x)}.DstAddr=={x.FirstIpAddress}"
                : $"({Ip(x)}.DstAddr>={x.FirstIpAddress} and {Ip(x)}.DstAddr<={x.LastIpAddress})");
            var phrase = string.Join(" or ", phrases);
            phraseX += $" and ({phrase})";
        }

        // add outbound; filter loopback
        var filter = $"(ip or ipv6) and outbound and !loopback and (udp.DstPort==53 or ({phraseX}))";
        // filter = $"(ip or ipv6) and outbound and !loopback and (protocol!=6 or tcp.DstPort!=3389) and (protocol!=6 or tcp.SrcPort!=3389) and (udp.DstPort==53 or ({phraseX}))";
        filter = filter.Replace("ipv6.DstAddr>=::", "ipv6"); // WinDivert bug
        try
        {
            _device.Filter = filter;
            _device.Open(new DeviceConfiguration());
            _device.StartCapture();
        }
        catch (Exception ex)
        {
            if (ex.Message.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new Exception(
                    "Access denied! Could not open WinDivert driver! Make sure the app is running with admin privilege.", ex);
            throw;
        }
    }

    public void StopCapture()
    {
        if (!Started)
            return;

        _device.StopCapture();
        OnStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopCapture();
            _device.Dispose();
            _disposed = true;
        }
    }

    private void Device_OnPacketArrival(object sender, PacketCapture e)
    {
        var rawPacket = e.GetPacket();
        var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
        var ipPacket = packet.Extract<IPPacket>();

        _lastCaptureHeader = (WinDivertHeader)e.Header;
        ProcessPacketReceivedFromInbound(ipPacket);
    }

    protected virtual void ProcessPacketReceivedFromInbound(IPPacket ipPacket)
    {
        try
        {
            var eventArgs = new PacketReceivedEventArgs(new[] { ipPacket }, this);
            OnPacketReceivedFromInbound?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            VhLogger.Instance.Log(LogLevel.Error, ex, 
                "Error in processing packet Packet: {Packet}", VhLogger.FormatIpPacket(ipPacket.ToString()));
        }
    }

    private void SendPacket(IPPacket ipPacket, bool outbound)
    {
        if (_lastCaptureHeader == null)
            throw new InvalidOperationException("Could not send any data without receiving a packet!");

        // send by a device
        _lastCaptureHeader.Flags = outbound ? WinDivertPacketFlags.Outbound : 0;
        _device.SendPacket(ipPacket.Bytes, _lastCaptureHeader);
    }

    #region Applications Filter

    public bool CanExcludeApps => false;
    public bool CanIncludeApps => false;

    public string[]? ExcludeApps
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public string[]? IncludeApps
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public bool IsMtuSupported => false;

    public int Mtu
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public bool IsAddIpV6AddressSupported => false;
    public bool AddIpV6Address
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    #endregion
}