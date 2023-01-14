﻿using System.Net;
using PacketDotNet;

namespace VpnHood.Tunneling;

public class EndPointEventPairArgs
{
    public ProtocolType ProtocolType { get; }
    public IPEndPoint LocalEndPoint { get; }
    public IPEndPoint RemoteEndPoint { get; }
    public bool IsNewLocalEndPoint { get; }
    public bool IsNewRemoteEndPoint { get; }

    public EndPointEventPairArgs(ProtocolType protocolType, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, 
        bool isNewLocalEndPoint, bool isNewRemoteEndPoint)
    {
        ProtocolType = protocolType;
        LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
        IsNewLocalEndPoint = isNewLocalEndPoint;
        IsNewRemoteEndPoint = isNewRemoteEndPoint;
        RemoteEndPoint = remoteEndPoint;
    }
}