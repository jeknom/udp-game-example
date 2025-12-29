using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Services;
using shared;

namespace udp_game_example;

public class UdpGameServerTests
{
    [Fact]
    public async Task UdpServer_SendsGameUpdates_AfterTwoPlayersReady()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddHostedService<UdpGameServer>();
            })
            .Build();
        
        await host.StartAsync();

        
        using var client1 = new UdpClient();
        using var client2 = new UdpClient();
        client1.Client.ReceiveTimeout = 3000;
        client2.Client.ReceiveTimeout = 3000;

        var serverEp = new IPEndPoint(IPAddress.Loopback, 11000);
        
        await client1.SendAsync([MessageTypes.JOIN_GAME], 1, serverEp);
        await client2.SendAsync([MessageTypes.JOIN_GAME], 1, serverEp);
        
        await client1.SendAsync([MessageTypes.PLAYER_READY], 1, serverEp);
        await client2.SendAsync([MessageTypes.PLAYER_READY], 1, serverEp);
        
        var remote1 = new IPEndPoint(IPAddress.Any, 0);
        var remote2 = new IPEndPoint(IPAddress.Any, 0);

        byte[] update1;
        byte[] update2;

        try
        {
            update1 = client1.Receive(ref remote1);
            update2 = client2.Receive(ref remote2);
        }
        finally
        {
            await host.StopAsync();
        }

        Assert.NotNull(update1);
        Assert.NotNull(update2);
        Assert.True(update1.Length >= 1);
        Assert.True(update2.Length >= 1);
        Assert.Equal(MessageTypes.GAME_UPDATE, update1[0]);
        Assert.Equal(MessageTypes.GAME_UPDATE, update2[0]);
        Assert.Equal(IPAddress.Loopback, remote1.Address);
        Assert.Equal(IPAddress.Loopback, remote2.Address);
        Assert.Equal(11000, remote1.Port);
        Assert.Equal(11000, remote2.Port);
    }
}