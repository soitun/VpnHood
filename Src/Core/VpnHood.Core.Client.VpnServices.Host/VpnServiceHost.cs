﻿using Microsoft.Extensions.Logging;
using System.Diagnostics;
using VpnHood.Core.Client.Abstractions;
using VpnHood.Core.Client.VpnServices.Abstractions;
using VpnHood.Core.Client.VpnServices.Abstractions.Tracking;
using VpnHood.Core.Toolkit.Logging;
using VpnHood.Core.Toolkit.Utils;
using VpnHood.Core.Tunneling;
using VpnHood.Core.Tunneling.Sockets;
using VpnHood.Core.VpnAdapters.Abstractions;

namespace VpnHood.Core.Client.VpnServices.Host;

public class VpnServiceHost : IDisposable
{
    private readonly ApiController _apiController;
    private readonly IVpnServiceHandler _vpnServiceHandler;
    private readonly ISocketFactory _socketFactory;
    private readonly LogService? _logService;
    private bool _disposed;
    private CancellationTokenSource _connectCts = new();

    internal VpnHoodClient? Client { get; private set; }
    internal VpnHoodClient RequiredClient => Client ?? throw new InvalidOperationException("Client is not initialized.");
    internal VpnServiceContext Context { get; }

    public VpnServiceHost(
        string configFolder,
        IVpnServiceHandler vpnServiceHandler,
        ISocketFactory socketFactory,
        bool withLogger = true)
    {
        Context = new VpnServiceContext(configFolder);
        _socketFactory = socketFactory;
        _vpnServiceHandler = vpnServiceHandler;
        _logService = withLogger ? new LogService(Context.LogFilePath) : null;
        VhLogger.TcpCloseEventId = GeneralEventId.TcpLife;

        // start apiController
        _apiController = new ApiController(this);
        VhLogger.Instance.LogInformation("VpnServiceHost has been initiated...");
    }

    private void VpnHoodClient_StateChanged(object? sender, EventArgs e)
    {
        var client = (VpnHoodClient?)sender;
        if (client == null)
            return;

        // update last sate
        VhLogger.Instance.LogDebug("VpnService update the connection info file. State:{State}, LastError: {LastError}",
            client.State, client.LastException?.Message);
        _ = Context.TryWriteConnectionInfo(client.ToConnectionInfo(_apiController), _connectCts.Token);

        // no client in progress, let's stop the service
        if (client.State is ClientState.Disposed) {
            // client is disposed or disconnecting, stop the notification and service
            VhLogger.Instance.LogDebug("VpnServiceHost requests to stop the notification and service.");
            _vpnServiceHandler.StopNotification();
            _vpnServiceHandler.StopSelf(); // it may be a red flag for android if we don't stop the service after stopping the notification
            return;
        }

        // show notification
        _vpnServiceHandler.ShowNotification(client.ToConnectionInfo(_apiController));
    }

    public async Task<bool> TryConnect(bool forceReconnect = false)
    {
        if (_disposed)
            return false;

        try {
            // handle previous client
            var client = Client;
            if (!forceReconnect && client is { State: ClientState.Connected or ClientState.Connecting or ClientState.Waiting }) {
                // user must disconnect first
                VhLogger.Instance.LogWarning("VpnService connection is already in progress.");
                await Context.TryWriteConnectionInfo(client.ToConnectionInfo(_apiController), _connectCts.Token).VhConfigureAwait();
                return false;
            }

            // cancel previous connection if exists
            _connectCts.Cancel();
            _connectCts.Dispose();
            if (client != null) {
                VhLogger.Instance.LogWarning("VpnService killing the previous connection.");

                // this prevents the previous connection to overwrite the state or stop the service
                client.StateChanged -= VpnHoodClient_StateChanged;

                // ReSharper disable once MethodHasAsyncOverload
                // Don't call disposeAsync here. We don't need graceful shutdown.
                // Graceful shutdown should be handled by disconnect or by client itself.
                client.Dispose();
                Client = null;
            }

            // start connecting 
            _connectCts = new CancellationTokenSource();
            await Connect(_connectCts.Token);
            return true;
        }
        catch (Exception ex) {
            VhLogger.Instance.LogError(ex, "VpnServiceHost could not establish the connection.");
            return false;
        }
    }

    private async Task Connect(CancellationToken cancellationToken)
    {
        try {
            var clientOptions = Context.ReadClientOptions();
            _logService?.Start(clientOptions.LogServiceOptions);

            // restart the log service
            VhLogger.Instance.LogInformation("VpnService is connecting... ProcessId: {ProcessId}", Process.GetCurrentProcess().Id);

            // create a connection info for notification
            var connectInfo = new ConnectionInfo {
                ClientState = ClientState.Initializing,
                ApiKey = _apiController.ApiKey,
                ApiEndPoint = _apiController.ApiEndPoint,
                SessionInfo = null,
                SessionStatus = null,
                Error = null,
                SessionName = clientOptions.SessionName,
                HasSetByService = true
            };

            // show notification as soon as possible
            _vpnServiceHandler.ShowNotification(connectInfo);

            // read client options and start log service
            _logService?.Start(clientOptions.LogServiceOptions);

            // sni is sensitive, must be explicitly enabled
            clientOptions.ForceLogSni |=
                clientOptions.LogServiceOptions.LogEventNames.Contains(nameof(GeneralEventId.Sni),
                    StringComparer.OrdinalIgnoreCase);

            // create tracker
            var trackerFactory = TryCreateTrackerFactory(clientOptions.TrackerFactoryAssemblyQualifiedName);
            var tracker = trackerFactory?.TryCreateTracker(new TrackerCreateParams {
                ClientId = clientOptions.ClientId,
                ClientVersion = clientOptions.Version,
                Ga4MeasurementId = clientOptions.Ga4MeasurementId,
                UserAgent = clientOptions.UserAgent
            });

            // create client
            VhLogger.Instance.LogDebug("VpnService is creating a new VpnHoodClient.");
            var adapterSetting = new VpnAdapterSettings {
                AdapterName = clientOptions.AppName,
                Blocking = false,
                AutoDisposePackets = true
            };

            var client = new VpnHoodClient(
                vpnAdapter: clientOptions.UseNullCapture
                    ? new NullVpnAdapter(autoDisposePackets: true, blocking: false)
                    : _vpnServiceHandler.CreateAdapter(adapterSetting, clientOptions.DebugData1),
                tracker: tracker,
                socketFactory: _socketFactory,
                options: clientOptions
            );
            client.StateChanged += VpnHoodClient_StateChanged;
            Client = client;

            // show notification.
            _vpnServiceHandler.ShowNotification(client.ToConnectionInfo(_apiController));

            // let connect in the background
            // ignore cancellation because it will be cancelled by disconnect or dispose
            await client.Connect(cancellationToken);
        }
        catch (Exception ex) {
            await Context.TryWriteConnectionInfo(_apiController.BuildConnectionInfo(ClientState.Disposed, ex), _connectCts.Token);
            _vpnServiceHandler.StopNotification();
            _vpnServiceHandler.StopSelf();
            throw;
        }
    }

    private static ITrackerFactory? TryCreateTrackerFactory(string? assemblyQualifiedName)
    {
        if (string.IsNullOrEmpty(assemblyQualifiedName))
            return null;

        try {
            var type = Type.GetType(assemblyQualifiedName);
            if (type == null)
                return null;

            var trackerFactory = Activator.CreateInstance(type) as ITrackerFactory;
            return trackerFactory;
        }
        catch (Exception ex) {
            VhLogger.Instance.LogError(ex, "Could not create tracker factory. ClassName: {className}", assemblyQualifiedName);
            return null;
        }
    }

    public async Task TryDisconnect()
    {
        if (_disposed)
            return;

        try {
            // let dispose in the background
            var client = Client;
            if (client != null)
                await client.DisposeAsync();
        }
        catch (Exception ex) {
            VhLogger.Instance.LogError(ex, "Could not disconnect the client.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // cancel connection if exists
        VhLogger.Instance.LogDebug("VpnService Host is destroying...");
        _connectCts.Cancel();
        _connectCts.Dispose();

        // dispose client
        var client = Client;
        if (client != null) {
            client.StateChanged -= VpnHoodClient_StateChanged; // after VpnHoodClient disposed
            client.Dispose();
        }

        // dispose api controller
        _apiController.Dispose();
        VhLogger.Instance.LogDebug("VpnService has been destroyed.");
        _logService?.Dispose();
    }
}

