﻿using Microsoft.Extensions.Logging;
using VpnHood.Client.Exceptions;
using VpnHood.Common.Exceptions;
using VpnHood.Common.Logging;
using VpnHood.Common.Messaging;
using VpnHood.Common.Utils;
using VpnHood.Tunneling;
using VpnHood.Tunneling.Factory;
using VpnHood.Tunneling.Messaging;

namespace VpnHood.Client.ConnectorServices;

internal class ConnectorService(ISocketFactory socketFactory, TimeSpan tcpTimeout)
    : ConnectorServiceBase(socketFactory, tcpTimeout)
{
    public async Task<ConnectorRequestResult<T>> SendRequest<T>(ClientRequest request, CancellationToken cancellationToken)
        where T : SessionResponseBase
    {
        var eventId = GetRequestEventId(request);
        VhLogger.Instance.LogTrace(eventId,
            "Sending a request. RequestCode: {RequestCode}, RequestId: {RequestId}",
            (RequestCode)request.RequestCode, request.RequestId);

        // set request timeout
        using var cancellationTokenSource = new CancellationTokenSource(RequestTimeout);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
        cancellationToken = linkedCancellationTokenSource.Token;

        await using var mem = new MemoryStream();
        mem.WriteByte(1);
        mem.WriteByte(request.RequestCode);
        await StreamUtil.WriteJsonAsync(mem, request, cancellationToken);
        var ret = await SendRequest<T>(mem.ToArray(), request.RequestId, cancellationToken);

        // log the response
        VhLogger.Instance.LogTrace(eventId, "Received a response... ErrorCode: {ErrorCode}.",
            ret.Response.ErrorCode);

        lock (Stat) Stat.RequestCount++;
        return ret;
    }

    private async Task<ConnectorRequestResult<T>> SendRequest<T>(byte[] request, string requestId, CancellationToken cancellationToken)
        where T : SessionResponseBase
    {
        // try reuse
        var clientStream = GetFreeClientStream();
        if (clientStream != null)
        {
            VhLogger.Instance.LogTrace(GeneralEventId.TcpLife,
                "A shared ClientStream has been reused. ClientStreamId: {ClientStreamId}, LocalEp: {LocalEp}",
                clientStream.ClientStreamId, clientStream.IpEndPointPair.LocalEndPoint);

            try
            {
                // we may use this buffer to encrypt so clone it for retry
                await clientStream.Stream.WriteAsync((byte[])request.Clone(), cancellationToken);
                var response = await ReadSessionResponse<T>(clientStream.Stream, cancellationToken);
                lock (Stat) Stat.ReusedConnectionSucceededCount++;
                return new ConnectorRequestResult<T>
                {
                    Response = response,
                    ClientStream = clientStream
                };
            }
            catch (Exception ex)
            {
                // dispose the connection and retry with new connection
                lock (Stat) Stat.ReusedConnectionFailedCount++;
                DisposingTasks.Add(clientStream.DisposeAsync(false));
                VhLogger.LogError(GeneralEventId.TcpLife, ex,
                    "Error in reusing the ClientStream. Try a new connection. ClientStreamId: {ClientStreamId}",
                    clientStream.ClientStreamId);
            }
        }

        // create free connection
        clientStream = await GetTlsConnectionToServer(requestId, cancellationToken);

        // send request
        try
        {
            await clientStream.Stream.WriteAsync(request, cancellationToken);
            var response2 = await ReadSessionResponse<T>(clientStream.Stream, cancellationToken);
            return new ConnectorRequestResult<T>
            {
                Response = response2,
                ClientStream = clientStream
            };
        }
        catch
        {
            DisposingTasks.Add(clientStream.DisposeAsync(false));
            throw;
        }
    }

    private static async Task<T> ReadSessionResponse<T>(Stream stream, CancellationToken cancellationToken) where T : SessionResponseBase
    {
        var message = await StreamUtil.ReadMessage(stream, cancellationToken);
        try
        {
            var response = VhUtil.JsonDeserialize<T>(message);
            ProcessResponseException(response);
            return response;
        }
        catch (Exception ex) when (ex is not SessionException)
        {
            var sessionResponse = VhUtil.JsonDeserialize<SessionResponse>(message);
            ProcessResponseException(sessionResponse);
            throw;
        }
    }

    private static void ProcessResponseException(SessionResponseBase response)
    {
        if (response.ErrorCode == SessionErrorCode.RedirectHost) throw new RedirectHostException(response);
        if (response.ErrorCode == SessionErrorCode.Maintenance) throw new MaintenanceException();
        if (response.ErrorCode != SessionErrorCode.Ok) throw new SessionException(response);
    }

    private static EventId GetRequestEventId(ClientRequest request)
    {
        return (RequestCode)request.RequestCode switch
        {
            RequestCode.Hello => GeneralEventId.Session,
            RequestCode.Bye => GeneralEventId.Session,
            RequestCode.TcpDatagramChannel => GeneralEventId.DatagramChannel,
            RequestCode.StreamProxyChannel => GeneralEventId.StreamProxyChannel,
            _ => GeneralEventId.Tcp
        };
    }

}