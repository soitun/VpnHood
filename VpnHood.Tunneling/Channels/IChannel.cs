﻿using System;
using System.Threading.Tasks;
using VpnHood.Common.Messaging;

namespace VpnHood.Tunneling.Channels;

public interface IChannel : IAsyncDisposable
{
    bool IsClosePending { get; }
    bool Connected { get; }
    DateTime LastActivityTime { get; }
    Traffic Traffic { get; }
    Task Start();
    event EventHandler<ChannelEventArgs> OnFinished;
}