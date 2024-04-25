﻿using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using VpnHood.Common.Exceptions;
using VpnHood.Common.Jobs;
using VpnHood.Common.Logging;
using VpnHood.Common.Messaging;
using VpnHood.Common.Net;
using VpnHood.Common.Utils;
using VpnHood.Server.Exceptions;
using VpnHood.Tunneling;
using VpnHood.Tunneling.Channels;
using VpnHood.Tunneling.Channels.Streams;
using VpnHood.Tunneling.ClientStreams;
using VpnHood.Tunneling.Messaging;
using VpnHood.Tunneling.Utils;

namespace VpnHood.Server;

internal class ServerHost : IAsyncDisposable, IJob
{
    private readonly HashSet<IClientStream> _clientStreams = [];
    private const int ServerProtocolVersion = 5;
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly SessionManager _sessionManager;
    private readonly List<TcpListener> _tcpListeners;
    private readonly List<UdpChannelTransmitter> _udpChannelTransmitters = [];
    private Task? _listenerTask;
    private bool _disposed;

    public JobSection JobSection { get; } = new(TimeSpan.FromMinutes(5));
    public bool IsIpV6Supported { get; set; }
    public IpRange[]? NetFilterPacketCaptureIncludeIpRanges { get; set; }
    public IpRange[]? NetFilterIncludeIpRanges { get; set; }
    public bool IsStarted { get; private set; }
    public IPEndPoint[] TcpEndPoints { get; private set; } = [];
    public IPEndPoint[] UdpEndPoints { get; private set; } = [];
    public IPAddress[]? DnsServers { get; set; }
    public CertificateHostName[] Certificates { get; private set; } = [];

    public ServerHost(SessionManager sessionManager)
    {
        _tcpListeners = [];
        _sessionManager = sessionManager;
        JobRunner.Default.Add(this);
    }

    private async Task Start(IPEndPoint[] tcpEndPoints, IPEndPoint[] udpEndPoints)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        if (IsStarted) throw new Exception("ServerHost is already Started!");
        if (tcpEndPoints.Length == 0) throw new ArgumentNullException(nameof(tcpEndPoints), "No TcpEndPoint has been configured.");

        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;
        IsStarted = true;
        lock (_stopLock) _stopTask = null;

        try
        {
            //start UDPs
            lock (_udpChannelTransmitters)
            {
                foreach (var udpEndPoint in udpEndPoints)
                {
                    if (udpEndPoint.Port != 0)
                        VhLogger.Instance.LogInformation("Start listening on UdpEndPoint: {UdpEndPoint}", VhLogger.Format(udpEndPoint));

                    try
                    {
                        var udpClient = new UdpClient(udpEndPoint);
                        var udpChannelTransmitter = new ServerUdpChannelTransmitter(udpClient, _sessionManager);
                        _udpChannelTransmitters.Add(udpChannelTransmitter);

                        if (udpEndPoint.Port == 0)
                            VhLogger.Instance.LogInformation("Started listening on UdpEndPoint: {UdpEndPoint}", VhLogger.Format(udpChannelTransmitter.LocalEndPoint));
                    }
                    catch (Exception ex)
                    {
                        ex.Data.Add("UdpEndPoint", udpEndPoint);
                        throw;
                    }
                }

                UdpEndPoints = udpEndPoints;
            }

            //start TCPs
            var tasks = new List<Task>();
            lock (_tcpListeners)
            {
                foreach (var tcpEndPoint in tcpEndPoints)
                {
                    try
                    {
                        VhLogger.Instance.LogInformation("Start listening on TcpEndPoint: {TcpEndPoint}", VhLogger.Format(tcpEndPoint));
                        cancellationToken.ThrowIfCancellationRequested();
                        var tcpListener = new TcpListener(tcpEndPoint);
                        tcpListener.Start();
                        _tcpListeners.Add(tcpListener);
                        tasks.Add(ListenTask(tcpListener, cancellationToken));
                    }
                    catch (Exception ex)
                    {
                        ex.Data.Add("TcpEndPoint", tcpEndPoint);
                        throw;
                    }
                }
            }
            TcpEndPoints = tcpEndPoints;
            _listenerTask = Task.WhenAll(tasks);
        }
        catch
        {
            await Stop();
            throw;
        }
    }

    private readonly AsyncLock _stopLock = new();
    private Task? _stopTask;

    public Task Stop()
    {
        lock (_stopLock)
            _stopTask ??= StopCore();

        return _stopTask;
    }

    private async Task StopCore()
    {
        if (!IsStarted)
            return;

        VhLogger.Instance.LogTrace("Stopping ServerHost...");
        _cancellationTokenSource.Cancel();

        // UDPs
        VhLogger.Instance.LogTrace("Disposing UdpChannelTransmitters...");
        lock (_udpChannelTransmitters)
        {
            foreach (var udpChannelClient in _udpChannelTransmitters)
                udpChannelClient.Dispose();
            _udpChannelTransmitters.Clear();
        }

        // TCPs
        VhLogger.Instance.LogTrace("Disposing TcpListeners...");
        lock (_tcpListeners)
        {
            foreach (var tcpListener in _tcpListeners)
                tcpListener.Stop();
            _tcpListeners.Clear();
        }

        // dispose clientStreams
        VhLogger.Instance.LogTrace("Disposing ClientStreams...");
        Task[] disposeTasks;
        lock (_clientStreams)
            disposeTasks = _clientStreams.Select(x => x.DisposeAsync(false).AsTask()).ToArray();
        await Task.WhenAll(disposeTasks);

        VhLogger.Instance.LogTrace("Disposing current processing requests...");
        try
        {
            if (_listenerTask != null)
                await _listenerTask;
        }
        catch (Exception ex)
        {
            VhLogger.Instance.LogTrace(ex, "Error in stopping ServerHost.");
        }

        _listenerTask = null;
        IsStarted = false;
    }

    public async Task Configure(IPEndPoint[] tcpEndPoints, IPEndPoint[] udpEndPoints,
        IPAddress[]? dnsServers, X509Certificate2[] certificates)
    {
        if (VhUtil.IsNullOrEmpty(certificates)) throw new ArgumentNullException(nameof(certificates), "Certificates has not been configured.");

        // Clear certificate cache
        DnsServers = dnsServers;
        Certificates = certificates.Select(x => new CertificateHostName(x)).ToArray();

        // Restart if endPoints has been changed
        if (IsStarted &&
            (!TcpEndPoints.SequenceEqual(tcpEndPoints) ||
             !UdpEndPoints.SequenceEqual(udpEndPoints)))
        {
            VhLogger.Instance.LogInformation("EndPoints has been changed. Stopping ServerHost...");
            await Stop();
        }

        if (!IsStarted)
        {
            VhLogger.Instance.LogInformation("Starting ServerHost...");
            await Start(tcpEndPoints, udpEndPoints);
        }
    }

    private async Task ListenTask(TcpListener tcpListener, CancellationToken cancellationToken)
    {
        var localEp = (IPEndPoint)tcpListener.LocalEndpoint;
        var errorCounter = 0;
        const int maxErrorCount = 200;

        // Listening for new connection
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                VhUtil.ConfigTcpClient(tcpClient,
                    _sessionManager.SessionOptions.TcpKernelSendBufferSize,
                    _sessionManager.SessionOptions.TcpKernelReceiveBufferSize);

                // config tcpClient
                _ = ProcessTcpClient(tcpClient, cancellationToken);
                errorCounter = 0;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                errorCounter++;
            }
            catch (ObjectDisposedException)
            {
                errorCounter++;
            }
            catch (Exception ex)
            {
                errorCounter++;
                VhLogger.Instance.LogError(GeneralEventId.Tcp, ex, "ServerHost could not AcceptTcpClient. ErrorCounter: {ErrorCounter}", errorCounter);
                if (errorCounter > maxErrorCount)
                {
                    VhLogger.Instance.LogError("Too many unexpected errors in AcceptTcpClient. Stopping the ServerHost...");
                    _ = Stop();
                }
            }
        }

        tcpListener.Stop();
        VhLogger.Instance.LogInformation("TCP Listener has been stopped. LocalEp: {LocalEp}", VhLogger.Format(localEp));
    }

    private async Task<SslStream> AuthenticateAsServerAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            VhLogger.Instance.LogTrace(GeneralEventId.Tcp, "TLS Authenticating...");
            var sslStream = new SslStream(stream, true);
            await sslStream.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ClientCertificateRequired = false,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    ServerCertificateSelectionCallback = ServerCertificateSelectionCallback
                },
                cancellationToken);
            return sslStream;
        }
        catch (Exception ex)
        {
            throw new TlsAuthenticateException("TLS Authentication error.", ex);
        }
    }

    private X509Certificate ServerCertificateSelectionCallback(object sender, string hostname)
    {
        var certificate = Certificates.SingleOrDefault(x => x.HostName.Equals(hostname, StringComparison.OrdinalIgnoreCase))
            ?? Certificates.First();

        return certificate.Certificate;
    }

    private bool CheckApiKeyAuthorization(string authorization)
    {
        var parts = authorization.Split(' ');
        return
            parts.Length >= 2 &&
            parts[0].Equals("ApiKey", StringComparison.OrdinalIgnoreCase) &&
            parts[1].Equals(_sessionManager.ApiKey, StringComparison.OrdinalIgnoreCase) &&
            parts[1] == _sessionManager.ApiKey;
    }

    private async Task<IClientStream> CreateClientStream(TcpClient tcpClient, Stream sslStream, CancellationToken cancellationToken)
    {
        VhLogger.Instance.LogTrace(GeneralEventId.Tcp, "Waiting for request...");
        var streamId = Guid.NewGuid() + ":incoming";

        // todo: deprecated on >= 451 {
        #region Deprecated on >= 451
        var buffer = new byte[16];
        var res = await sslStream.ReadAsync(buffer, 0, 1, cancellationToken);
        if (res == 0)
            throw new Exception("Connection has been closed before receiving any request.");

        // check request version
        var version = buffer[0];
        if (version == 1)
            return new TcpClientStream(tcpClient, new ReadCacheStream(sslStream, false, cacheData: [version], cacheSize: 1), streamId);
        #endregion

        // Version 2 is HTTP and starts with POST
        try
        {
            var headers =
                await HttpUtil.ParseHeadersAsync(sslStream, cancellationToken)
                ?? throw new Exception("Connection has been closed before receiving any request.");

            int.TryParse(headers.GetValueOrDefault("X-Version", "0"), out var xVersion);
            Enum.TryParse<BinaryStreamType>(headers.GetValueOrDefault("X-BinaryStream", ""), out var binaryStreamType);
            bool.TryParse(headers.GetValueOrDefault("X-Buffered", "true"), out var useBuffer);
            var authorization = headers.GetValueOrDefault("Authorization", string.Empty);
            if (xVersion == 2) binaryStreamType = BinaryStreamType.Custom;

            // read api key
            if (!CheckApiKeyAuthorization(authorization))
            {
                // process hello without api key
                if (authorization != "ApiKey")
                    throw new UnauthorizedAccessException();

                await sslStream.WriteAsync(HttpResponseBuilder.Unauthorized(), cancellationToken);
                return new TcpClientStream(tcpClient, sslStream, streamId);
            }

            // use binary stream only for authenticated clients
            await sslStream.WriteAsync(HttpResponseBuilder.Ok(), cancellationToken);

            switch (binaryStreamType)
            {
                case BinaryStreamType.Custom:
                    {
                        await sslStream.DisposeAsync(); // dispose Ssl
                        var xSecret = headers.GetValueOrDefault("X-Secret", string.Empty);
                        var secret = Convert.FromBase64String(xSecret);
                        return new TcpClientStream(tcpClient, new BinaryStreamCustom(tcpClient.GetStream(), streamId, secret, useBuffer), streamId, ReuseClientStream);
                    }

                case BinaryStreamType.Standard:
                    return new TcpClientStream(tcpClient, new BinaryStreamStandard(tcpClient.GetStream(), streamId, useBuffer), streamId, ReuseClientStream);

                case BinaryStreamType.None:
                    return new TcpClientStream(tcpClient, sslStream, streamId);

                case BinaryStreamType.Unknown:
                default:
                    throw new NotSupportedException("Unknown BinaryStreamType");
            }
        }
        catch (Exception ex)
        {
            //always return BadRequest 
            if (!VhUtil.IsTcpClientHealthy(tcpClient)) throw;
            var response = ex is UnauthorizedAccessException ? HttpResponseBuilder.Unauthorized() : HttpResponseBuilder.BadRequest();
            await sslStream.WriteAsync(response, cancellationToken);
            throw;
        }
    }

    private async Task ProcessTcpClient(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        // add timeout to cancellationToken
        using var timeoutCt = new CancellationTokenSource(_sessionManager.SessionOptions.TcpReuseTimeoutValue);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCt.Token, cancellationToken);
        cancellationToken = cancellationTokenSource.Token;

        IClientStream? clientStream = null;
        try
        {
            // establish SSL
            var sslStream = await AuthenticateAsServerAsync(tcpClient.GetStream(), cancellationToken);

            // create client stream
            clientStream = await CreateClientStream(tcpClient, sslStream, cancellationToken);
            lock (_clientStreams) _clientStreams.Add(clientStream);

            await ProcessClientStream(clientStream, cancellationToken);
        }
        catch (TlsAuthenticateException ex) when (ex.InnerException is OperationCanceledException)
        {
            VhLogger.Instance.LogInformation(GeneralEventId.Tls, "Client TLS authentication has been canceled.");
            tcpClient.Dispose();
        }
        catch (TlsAuthenticateException ex)
        {
            VhLogger.Instance.LogInformation(GeneralEventId.Tls, ex, "Error in Client TLS authentication.");
            tcpClient.Dispose();
        }
        catch (Exception ex)
        {
            if (ex is ISelfLog loggable) loggable.Log();
            else VhLogger.LogError(GeneralEventId.Request, ex,
                "ServerHost could not process this request. ClientStreamId: {ClientStreamId}", clientStream?.ClientStreamId);

            if (clientStream != null) await clientStream.DisposeAsync(false);
            tcpClient.Dispose();
        }
    }

    private async Task ReuseClientStream(IClientStream clientStream)
    {
        lock (_clientStreams) _clientStreams.Add(clientStream);
        using var timeoutCt = new CancellationTokenSource(_sessionManager.SessionOptions.TcpReuseTimeoutValue);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCt.Token, _cancellationTokenSource.Token);
        var cancellationToken = cancellationTokenSource.Token;

        // don't add new client in disposing
        if (cancellationToken.IsCancellationRequested)
        {
            _ = clientStream.DisposeAsync(false);
            return;
        }

        // process incoming client
        try
        {
            VhLogger.Instance.LogTrace(GeneralEventId.TcpLife,
                "ServerHost.ReuseClientStream: A shared ClientStream is pending for reuse. ClientStreamId: {ClientStreamId}",
                clientStream.ClientStreamId);

            await ProcessClientStream(clientStream, cancellationToken);

            VhLogger.Instance.LogTrace(GeneralEventId.TcpLife,
                "ServerHost.ReuseClientStream: A shared ClientStream has been reused. ClientStreamId: {ClientStreamId}",
                clientStream.ClientStreamId);
        }
        catch (Exception ex)
        {
            VhLogger.LogError(GeneralEventId.Tcp, ex,
                "ServerHost.ReuseClientStream: Could not reuse a ClientStream. ClientStreamId: {ClientStreamId}",
                clientStream.ClientStreamId);

            await clientStream.DisposeAsync(false);
        }
    }

    private async Task ProcessClientStream(IClientStream clientStream, CancellationToken cancellationToken)
    {
        using var scope = VhLogger.Instance.BeginScope($"RemoteEp: {VhLogger.Format(clientStream.IpEndPointPair.RemoteEndPoint)}");
        try
        {
            await ProcessRequest(clientStream, cancellationToken);
        }
        catch (SessionException ex)
        {
            // reply the error to caller if it is SessionException
            // Should not reply anything when user is unknown
            await StreamUtil.WriteJsonAsync(clientStream.Stream, ex.SessionResponse,
                cancellationToken);

            if (ex is ISelfLog loggable)
                loggable.Log();
            else
                VhLogger.Instance.LogInformation(ex.SessionResponse.ErrorCode == SessionErrorCode.GeneralError ? GeneralEventId.Tcp : GeneralEventId.Session, ex,
                    "Could not process the request. SessionErrorCode: {SessionErrorCode}", ex.SessionResponse.ErrorCode);

            await clientStream.DisposeAsync();
        }
        catch (Exception ex) when (VhLogger.IsSocketCloseException(ex))
        {
            VhLogger.LogError(GeneralEventId.Tcp, ex,
                "Connection has been closed. ClientStreamId: {ClientStreamId}.",
                clientStream.ClientStreamId);

            await clientStream.DisposeAsync();
        }
        catch (Exception ex)
        {
            // return 401 for ANY non SessionException to keep server's anonymity
            await clientStream.Stream.WriteAsync(HttpResponseBuilder.Unauthorized(), cancellationToken);

            if (ex is ISelfLog loggable)
                loggable.Log();
            else
                VhLogger.Instance.LogInformation(GeneralEventId.Tcp, ex,
                    "Could not process the request and return 401. ClientStreamId: {ClientStreamId}", clientStream.ClientStreamId);

            await clientStream.DisposeAsync(false);
        }
        finally
        {
            lock (_clientStreams)
                _clientStreams.Remove(clientStream);
        }
    }

    private async Task ProcessRequest(IClientStream clientStream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];

        // read request version
        var rest = await clientStream.Stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        if (rest == 0)
            throw new Exception("ClientStream has been closed before receiving any request.");

        var version = buffer[0];
        if (version != 1)
            throw new NotSupportedException($"The request version is not supported. Version: {version}");

        // read request code
        var res = await clientStream.Stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        if (res == 0)
            throw new Exception("ClientStream has been closed before receiving any request.");

        var requestCode = (RequestCode)buffer[0];
        switch (requestCode)
        {
            case RequestCode.Hello:
                await ProcessHello(clientStream, cancellationToken);
                break;

            case RequestCode.TcpDatagramChannel:
                await ProcessTcpDatagramChannel(clientStream, cancellationToken);
                break;

            case RequestCode.StreamProxyChannel:
                await ProcessStreamProxyChannel(clientStream, cancellationToken);
                break;

            case RequestCode.UdpPacket:
                await ProcessUdpPacketRequest(clientStream, cancellationToken);
                break;

            case RequestCode.AdReward:
                await ProcessAdRewardRequest(clientStream, cancellationToken);
                break;

            case RequestCode.Bye:
                await ProcessBye(clientStream, cancellationToken);
                break;

            default:
                throw new NotSupportedException($"Unknown requestCode. requestCode: {requestCode}");
        }
    }

    private static async Task<T> ReadRequest<T>(IClientStream clientStream, CancellationToken cancellationToken) where T : ClientRequest
    {
        // reading request
        VhLogger.Instance.LogTrace(GeneralEventId.Session,
            "Processing a request. RequestType: {RequestType}.",
            VhLogger.FormatType<T>());

        var request = await StreamUtil.ReadJsonAsync<T>(clientStream.Stream, cancellationToken);
        request.RequestId = request.RequestId.Replace(":client", ":server");
        clientStream.ClientStreamId = request.RequestId;

        VhLogger.Instance.LogTrace(GeneralEventId.Session,
            "Request has been read. RequestType: {RequestType}. RequestId: {RequestId}",
            VhLogger.FormatType<T>(), request.RequestId);

        return request;
    }

    private async Task ProcessHello(IClientStream clientStream, CancellationToken cancellationToken)
    {
        var ipEndPointPair = clientStream.IpEndPointPair;

        // reading request
        VhLogger.Instance.LogTrace(GeneralEventId.Session, "Processing hello request... ClientIp: {ClientIp}",
            VhLogger.Format(ipEndPointPair.RemoteEndPoint.Address));

        var request = await ReadRequest<HelloRequest>(clientStream, cancellationToken);

        // creating a session
        VhLogger.Instance.LogTrace(GeneralEventId.Session, "Creating a session... TokenId: {TokenId}, ClientId: {ClientId}, ClientVersion: {ClientVersion}, UserAgent: {UserAgent}",
            VhLogger.FormatId(request.TokenId), VhLogger.FormatId(request.ClientInfo.ClientId), request.ClientInfo.ClientVersion, request.ClientInfo.UserAgent);
        var sessionResponse = await _sessionManager.CreateSession(request, ipEndPointPair);
        var session = _sessionManager.GetSessionById(sessionResponse.SessionId) ?? throw new InvalidOperationException("Session is lost!");

        // check client version; unfortunately it must be after CreateSession to preserve server anonymity
        if (request.ClientInfo == null || request.ClientInfo.ProtocolVersion < 4)
            throw new ServerSessionException(clientStream.IpEndPointPair.RemoteEndPoint, session, SessionErrorCode.UnsupportedClient, request.RequestId,
                "This client is outdated and not supported anymore! Please update your app.");

        // Report new session
        var clientIp = _sessionManager.TrackingOptions.TrackClientIpValue ? VhLogger.Format(ipEndPointPair.RemoteEndPoint.Address) : "*";

        // report in main log
        VhLogger.Instance.LogInformation(GeneralEventId.Session, "New Session. SessionId: {SessionId}, Agent: {Agent}", VhLogger.FormatSessionId(session.SessionId), request.ClientInfo.UserAgent);

        // report in session log
        VhLogger.Instance.LogInformation(GeneralEventId.SessionTrack,
            "SessionId: {SessionId-5}\t{Mode,-5}\tTokenId: {TokenId}\tClientCount: {ClientCount,-3}\tClientId: {ClientId}\tClientIp: {ClientIp-15}\tVersion: {Version}\tOS: {OS}",
            VhLogger.FormatSessionId(session.SessionId), "New", VhLogger.FormatId(request.TokenId), session.SessionResponse.AccessUsage?.ActiveClientCount, VhLogger.FormatId(request.ClientInfo.ClientId), clientIp, request.ClientInfo.ClientVersion, UserAgentParser.GetOperatingSystem(request.ClientInfo.UserAgent));

        // report in track log
        if (_sessionManager.TrackingOptions.IsEnabled)
        {
            VhLogger.Instance.LogInformation(GeneralEventId.Track,
                "{Proto}; SessionId {SessionId}; TokenId {TokenId}; ClientIp {clientIp}".Replace("; ", "\t"),
                "NewS", VhLogger.FormatSessionId(session.SessionId), VhLogger.FormatId(request.TokenId), clientIp);
        }

        // reply hello session
        VhLogger.Instance.LogTrace(GeneralEventId.Session,
            $"Replying Hello response. SessionId: {VhLogger.FormatSessionId(sessionResponse.SessionId)}");

        var udpPort =
            UdpEndPoints.SingleOrDefault(x => x.Address.Equals(ipEndPointPair.LocalEndPoint.Address))?.Port ??
            UdpEndPoints.SingleOrDefault(x => x.Address.Equals(IPAddressUtil.GetAnyIpAddress(ipEndPointPair.LocalEndPoint.AddressFamily)))?.Port;

        var helloResponse = new HelloResponse
        {
            ErrorCode = sessionResponse.ErrorCode,
            ErrorMessage = sessionResponse.ErrorMessage,
            AccessUsage = sessionResponse.AccessUsage,
            SuppressedBy = sessionResponse.SuppressedBy,
            RedirectHostEndPoint = sessionResponse.RedirectHostEndPoint,
            SessionId = sessionResponse.SessionId,
            SessionKey = sessionResponse.SessionKey,
            ServerSecret = _sessionManager.ServerSecret,
            UdpPort = udpPort,
            GaMeasurementId = sessionResponse.GaMeasurementId,
            ServerVersion = _sessionManager.ServerVersion.ToString(3),
            ServerProtocolVersion = ServerProtocolVersion,
            SuppressedTo = sessionResponse.SuppressedTo,
            MaxDatagramChannelCount = session.Tunnel.MaxDatagramChannelCount,
            ClientPublicAddress = ipEndPointPair.RemoteEndPoint.Address,
            IncludeIpRanges = NetFilterIncludeIpRanges,
            PacketCaptureIncludeIpRanges = NetFilterPacketCaptureIncludeIpRanges,
            IsIpV6Supported = IsIpV6Supported,
            // client should wait more to get session exception replies
            RequestTimeout = _sessionManager.SessionOptions.TcpConnectTimeoutValue + TunnelDefaults.ClientRequestTimeoutDelta,
            // client should wait less to make sure server is not closing the connection
            TcpReuseTimeout = _sessionManager.SessionOptions.TcpReuseTimeoutValue - TunnelDefaults.ClientRequestTimeoutDelta,
            AccessKey = sessionResponse.AccessKey,
            DnsServers = DnsServers,
            IsAdRequired = sessionResponse.IsAdRequired
        };
        await StreamUtil.WriteJsonAsync(clientStream.Stream, helloResponse, cancellationToken);
        await clientStream.DisposeAsync();
    }

    private async Task ProcessAdRewardRequest(IClientStream clientStream, CancellationToken cancellationToken)
    {
        VhLogger.Instance.LogTrace(GeneralEventId.Session, "Reading the RewardAd request...");
        var request = await ReadRequest<AdRewardRequest>(clientStream, cancellationToken);
        var session = await _sessionManager.GetSession(request, clientStream.IpEndPointPair);
        await session.ProcessAdRewardRequest(request, clientStream, cancellationToken);
    }

    private async Task ProcessBye(IClientStream clientStream, CancellationToken cancellationToken)
    {
        VhLogger.Instance.LogTrace(GeneralEventId.Session, "Reading the Bye request...");
        var request = await ReadRequest<ByeRequest>(clientStream, cancellationToken);

        // finding session
        using var scope = VhLogger.Instance.BeginScope($"SessionId: {VhLogger.FormatSessionId(request.SessionId)}");
        var session = await _sessionManager.GetSession(request, clientStream.IpEndPointPair);

        // Before calling CloseSession. Session must be validated by GetSession
        await StreamUtil.WriteJsonAsync(clientStream.Stream, new SessionResponse { ErrorCode = SessionErrorCode.Ok }, cancellationToken);
        await clientStream.DisposeAsync(false);

        // must be last
        await _sessionManager.CloseSession(session.SessionId);
    }

    private async Task ProcessUdpPacketRequest(IClientStream clientStream, CancellationToken cancellationToken)
    {
        VhLogger.Instance.LogTrace(GeneralEventId.Session, "Reading a UdpPacket request...");
        var request = await ReadRequest<UdpPacketRequest>(clientStream, cancellationToken);

        using var scope = VhLogger.Instance.BeginScope($"SessionId: {VhLogger.FormatSessionId(request.SessionId)}");
        var session = await _sessionManager.GetSession(request, clientStream.IpEndPointPair);
        await session.ProcessUdpPacketRequest(request, clientStream, cancellationToken);
    }


    private async Task ProcessTcpDatagramChannel(IClientStream clientStream, CancellationToken cancellationToken)
    {
        VhLogger.Instance.LogTrace(GeneralEventId.StreamProxyChannel, "Reading the TcpDatagramChannelRequest...");
        var request = await ReadRequest<TcpDatagramChannelRequest>(clientStream, cancellationToken);

        // finding session
        using var scope = VhLogger.Instance.BeginScope($"SessionId: {VhLogger.FormatSessionId(request.SessionId)}");
        var session = await _sessionManager.GetSession(request, clientStream.IpEndPointPair);
        await session.ProcessTcpDatagramChannelRequest(request, clientStream, cancellationToken);
    }

    private async Task ProcessStreamProxyChannel(IClientStream clientStream, CancellationToken cancellationToken)
    {
        VhLogger.Instance.LogInformation(GeneralEventId.StreamProxyChannel, "Reading the StreamProxyChannelRequest...");
        var request = await ReadRequest<StreamProxyChannelRequest>(clientStream, cancellationToken);

        // find session
        using var scope = VhLogger.Instance.BeginScope($"SessionId: {VhLogger.FormatSessionId(request.SessionId)}");
        var session = await _sessionManager.GetSession(request, clientStream.IpEndPointPair);
        await session.ProcessTcpProxyRequest(request, clientStream, cancellationToken);
    }

    public Task RunJob()
    {
        lock (_clientStreams)
            _clientStreams.RemoveWhere(x => x.Disposed);

        return Task.CompletedTask;
    }

    private readonly AsyncLock _disposeLock = new();
    private ValueTask? _disposeTask;
    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
            _disposeTask ??= DisposeAsyncCore();
        return _disposeTask.Value;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;
        _disposed = true;

        await Stop();
    }

    public class CertificateHostName(X509Certificate2 certificate)
    {
        public string HostName { get; } = certificate.GetNameInfo(X509NameType.DnsName, false) ?? throw new Exception("Could not get the HostName from the certificate.");
        public X509Certificate2 Certificate { get; } = certificate;
    }
}
