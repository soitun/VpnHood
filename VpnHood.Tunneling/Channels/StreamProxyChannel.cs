﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VpnHood.Common.JobController;
using VpnHood.Common.Logging;
using VpnHood.Common.Messaging;
using VpnHood.Common.Utils;
using VpnHood.Tunneling.ClientStreams;

namespace VpnHood.Tunneling.Channels;

public class StreamProxyChannel : IChannel, IJob
{
    private readonly int _orgStreamBufferSize;
    private readonly IClientStream _orgTcpClientStream;
    private readonly int _tunnelStreamBufferSize;
    private readonly IClientStream _tunnelTcpClientStream;
    private const int BufferSizeDefault = 0x1000 * 4; //16k
    private const int BufferSizeMax = 0x14000;
    private const int BufferSizeMin = 0x1000;
    private bool _disposed;

    public JobSection JobSection { get; }
    public event EventHandler<ChannelEventArgs>? OnFinished;
    public bool IsClosePending => false;
    public bool Connected { get; private set; }
    public Traffic Traffic { get; } = new();
    public DateTime LastActivityTime { get; private set; } = FastDateTime.Now;

    public StreamProxyChannel(IClientStream orgClientStream, IClientStream tunnelClientStream,
        TimeSpan tcpTimeout, int? orgStreamBufferSize = BufferSizeDefault, int? tunnelStreamBufferSize = BufferSizeDefault)
    {
        _orgTcpClientStream = orgClientStream ?? throw new ArgumentNullException(nameof(orgClientStream));
        _tunnelTcpClientStream = tunnelClientStream ?? throw new ArgumentNullException(nameof(tunnelClientStream));

        // validate buffer sizes
        if (orgStreamBufferSize is 0 or null) orgStreamBufferSize = BufferSizeDefault;
        if (tunnelStreamBufferSize is 0 or null) tunnelStreamBufferSize = BufferSizeDefault;

        _orgStreamBufferSize = orgStreamBufferSize is >= BufferSizeMin and <= BufferSizeMax
            ? orgStreamBufferSize.Value
            : throw new ArgumentOutOfRangeException(nameof(orgStreamBufferSize), orgStreamBufferSize,
                $"Value must be greater or equal than {BufferSizeMin} and less than {BufferSizeMax}.");

        _tunnelStreamBufferSize = tunnelStreamBufferSize is >= BufferSizeMin and <= BufferSizeMax
            ? tunnelStreamBufferSize.Value
            : throw new ArgumentOutOfRangeException(nameof(tunnelStreamBufferSize), tunnelStreamBufferSize,
                $"Value must be greater or equal than {BufferSizeMin} and less than {BufferSizeMax}");

        // We don't know about client or server delay, so lets pessimistic
        orgClientStream.NoDelay = true;
        tunnelClientStream.NoDelay = true;

        JobSection = new JobSection(tcpTimeout);
        JobRunner.Default.Add(this);
    }

    public async Task Start()
    {
        Connected = true;
        try
        {
            var task1 = CopyToAsync(_tunnelTcpClientStream.Stream, _orgTcpClientStream.Stream, false, _tunnelStreamBufferSize, CancellationToken.None); // read
            var task2 = CopyToAsync(_orgTcpClientStream.Stream, _tunnelTcpClientStream.Stream, true, _orgStreamBufferSize, CancellationToken.None); //write
            await Task.WhenAny(task1, task2);
        }
        finally
        {
            await DisposeAsync();
            OnFinished?.Invoke(this, new ChannelEventArgs(this));
        }
    }

    public async Task RunJob()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);

        await CheckClientIsAlive();
    }

    private async Task CheckClientIsAlive()
    {
        if (_orgTcpClientStream.CheckIsAlive() && _tunnelTcpClientStream.CheckIsAlive())
            return;

        VhLogger.Instance.LogInformation(GeneralEventId.TcpProxyChannel,
            $"Disposing a {VhLogger.FormatType(this)} due to its error state.");

        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Connected = false;
        await _orgTcpClientStream.DisposeAsync();
        await _tunnelTcpClientStream.DisposeAsync();
    }

    private async Task CopyToAsync(Stream source, Stream destination, bool isSendingOut, int bufferSize,
        CancellationToken cancellationToken)
    {
        try
        {
            await CopyToInternalAsync(source, destination, isSendingOut, bufferSize, cancellationToken);
        }
        catch (Exception ex)
        {
            // Dispose if any side throw an exception
            if (!_disposed)
            {
                var message = isSendingOut ? "to" : "from";
                VhLogger.Instance.LogInformation(GeneralEventId.Tcp, ex, $"TcpProxyChannel: Error in copying {message} tunnel.");
                await DisposeAsync();
            }
        }
    }

    private async Task CopyToInternalAsync(Stream source, Stream destination, bool isSendingOut, int bufferSize,
        CancellationToken cancellationToken)
    {
        var doubleBuffer = false; //i am not sure it could help!

        // Microsoft Stream Source Code:
        // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        // The CopyTo/CopyToAsync buffer is short-lived and is likely to be collected at Gen0, and it offers a significant
        // improvement in Copy performance.
        // 0x14000 recommended by microsoft for copying buffers
        if (bufferSize > BufferSizeMax)
            throw new ArgumentException($"Buffer is too big, maximum supported size is {BufferSizeMax}",
                nameof(bufferSize));

        // <<----------------- the MOST memory consuming in the APP! >> ----------------------
        var readBuffer = new byte[bufferSize];
        var writeBuffer = doubleBuffer ? new byte[readBuffer.Length] : null;
        Task? writeTask = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            // read from source
            var bytesRead = await source.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken);
            if (writeTask != null)
                await writeTask;

            // check end of the stream
            if (bytesRead == 0)
                break;

            // write to destination
            if (writeBuffer != null)
            {
                Array.Copy(readBuffer, writeBuffer, bytesRead);
                writeTask = destination.WriteAsync(writeBuffer, 0, bytesRead, cancellationToken);
            }
            else
            {
                await destination.WriteAsync(readBuffer, 0, bytesRead, cancellationToken);
            }

            // calculate transferred bytes
            if (!isSendingOut)
                Traffic.Received += bytesRead;
            else
                Traffic.Sent += bytesRead;

            // set LastActivityTime as some data delegated
            LastActivityTime = FastDateTime.Now;
        }
    }
}