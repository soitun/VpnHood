﻿using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

namespace VpnHood.Test;

public class TestWebServer : IDisposable
{
    private readonly WebServer _webServer;
    public IPEndPoint[] HttpsEndPoints { get; } = {
        IPEndPoint.Parse("127.10.1.1:15001"),
        IPEndPoint.Parse("127.10.1.1:15002"),
        IPEndPoint.Parse("127.10.1.1:15003"),
        IPEndPoint.Parse("127.10.1.1:15004"),
    };

    public IPEndPoint[] HttpEndPoints { get; } = {
        IPEndPoint.Parse("127.10.1.1:15005"),
        IPEndPoint.Parse("127.10.1.1:15006"),
        IPEndPoint.Parse("127.10.1.1:15007"),
        IPEndPoint.Parse("127.10.1.1:15008"),
    };

    public IPEndPoint UdpEndPoint1 = IPEndPoint.Parse("127.10.1.1:20101");
    public IPEndPoint UdpEndPoint2 = IPEndPoint.Parse("127.10.1.1:20102");
    public IPEndPoint UdpEndPoint3 = IPEndPoint.Parse("127.10.1.1:20103");
    public IPEndPoint UdpEndPoint4 = IPEndPoint.Parse("127.10.1.1:20104");

    public IPEndPoint UdpEndPoint1Ip6 = IPEndPoint.Parse("[::1]:20101");
    public IPEndPoint UdpEndPoint2Ip6 = IPEndPoint.Parse("[::1]:20102");
    public IPEndPoint UdpEndPoint3Ip6 = IPEndPoint.Parse("[::1]:20103");
    public IPEndPoint UdpEndPoint4Ip6 = IPEndPoint.Parse("[::1]:20104");

    private IPEndPoint[] UdpEndPointsIp4 => new []
    {
        UdpEndPoint1,
        UdpEndPoint2,
        UdpEndPoint3,
        UdpEndPoint4,
    };

    private IPEndPoint[] UdpEndPointsIp6 => new[]
    {
        UdpEndPoint1Ip6,
        UdpEndPoint2Ip6,
        UdpEndPoint3Ip6,
        UdpEndPoint4Ip6,
    };


    public Uri[] HttpUrls { get; }
    public Uri[] HttpsUrls { get; }

    public string FileContent1;
    public string FileContent2;

    private UdpClient[] UdpClients { get; }
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CancellationToken CancellationToken => _cancellationTokenSource.Token;
    private TestWebServer()
    {
        HttpUrls = HttpEndPoints.Select(x => new Uri($"http://{x}/file1")).ToArray();
        HttpsUrls = HttpsEndPoints.Select(x => new Uri($"https://{x}/file1")).ToArray();
        UdpClients = UdpEndPointsIp4.Concat(UdpEndPointsIp6).Select(x => new UdpClient(x)).ToArray();

        // Init files
        FileContent1 = string.Empty;
        FileContent2 = string.Empty;
        for (var i = 0; i < 100; i++)
        {
            FileContent1 += Guid.NewGuid().ToString();
            FileContent2 += Guid.NewGuid().ToString();
        }

        // Create web server
        var webServerOptions = new WebServerOptions
        {
            Certificate = new X509Certificate2("Assets/VpnHood.UnitTest.pfx", (string?)null, X509KeyStorageFlags.Exportable),
            AutoRegisterCertificate = false,
            Mode = HttpListenerMode.EmbedIO
        };

        foreach (var endpoint in HttpEndPoints)
            webServerOptions.AddUrlPrefix($"http://{endpoint}");

        foreach (var endpoint in HttpsEndPoints)
            webServerOptions.AddUrlPrefix($"https://{endpoint}");

        _webServer = new WebServer(webServerOptions)
            .WithWebApi("/", c => c.WithController(() => new ApiController(this)));
    }

    public static TestWebServer Create()
    {
        var ret = new TestWebServer();
        ret._webServer.Start();
        ret.StartUdpEchoServer();
        return ret;
    }

    private void StartUdpEchoServer()
    {
        foreach (var udpClient in UdpClients)
        {
            udpClient.Client.IOControl(-1744830452, new byte[] { 0 }, new byte[] { 0 });
            _ = StartUdpEchoServer(udpClient);
        }
    }

    private async Task StartUdpEchoServer(UdpClient udpClient)
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            var udpResult = await udpClient.ReceiveAsync(CancellationToken);
            await udpClient.SendAsync(udpResult.Buffer, udpResult.RemoteEndPoint, CancellationToken);
        }
    }


    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _webServer.Dispose();
        foreach (var udpClient in UdpClients)
            udpClient.Dispose();
    }

    private class ApiController : WebApiController
    {
        private readonly TestWebServer _testWebServer;

        public ApiController(TestWebServer testWebServer)
        {
            _testWebServer = testWebServer;
        }

        [Route(HttpVerbs.Get, "/file1")]
        public Task<string> File1()
        {
            return Task.FromResult(_testWebServer.FileContent1);
        }

        [Route(HttpVerbs.Get, "/file2")]
        public Task<string> File2()
        {
            return Task.FromResult(_testWebServer.FileContent1);
        }
    }
}