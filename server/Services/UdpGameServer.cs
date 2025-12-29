using System.Net;
using System.Net.Sockets;

namespace Server.Services;

public class UdpGameServer : BackgroundService
{
    private readonly ILogger<UdpGameServer> _logger;

    public UdpGameServer(ILogger<UdpGameServer> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 11000));
        _logger.LogInformation("UDP Game Server listening on port {Port}", 11000);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await udpClient.ReceiveAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving UDP packet");
                    continue;
                }
                
                try
                {
                    await udpClient.SendAsync(result.Buffer, result.Buffer.Length, result.RemoteEndPoint);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending UDP packet");
                }
            }
        }
        finally
        {
            _logger.LogInformation("UDP Game Server is stopping.");
        }
    }
}

