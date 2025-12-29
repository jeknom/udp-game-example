using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Services;

namespace udp_game_example;

public class UdpGameServerTests
{
    [Fact]
    public async Task UdpEchoServer_EchoesBackData()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddHostedService<UdpGameServer>();
            })
            .Build();
        
        await host.StartAsync();

        var payload = new byte[] { 1, 2, 3, 4, 5 };

        using var client = new UdpClient();
        client.Client.ReceiveTimeout = 2000;

        await client.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, 11000));

        var remoteEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] echoed;
        try
        {
            echoed = client.Receive(ref remoteEP);
        }
        finally
        {
            await host.StopAsync();
        }

        Assert.Equal(payload, echoed);
        Assert.Equal(IPAddress.Loopback, remoteEP.Address);
        Assert.Equal(11000, remoteEP.Port);
    }
}