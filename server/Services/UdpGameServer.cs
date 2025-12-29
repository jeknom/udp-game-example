using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Diagnostics;
using shared;

namespace Server.Services;

public class UdpGameServer : BackgroundService
{
    private readonly ILogger<UdpGameServer> _logger;
    private GameState _gameState = GameState.WaitingForPlayersToConnect;
    private PlayerState? _player1;
    private PlayerState? _player2;
    private const int PORT = 11000;
    
    private const int TICKS_PER_SECOND = 60;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1.0 / TICKS_PER_SECOND);
    private const int MAX_CATCH_UPS_PER_LOOP = 5;
    private const int SLOW_TICK_EVERY_FRAMES = 60;
    
    private readonly ConcurrentQueue<(byte[] Buffer, IPEndPoint RemoteEndPoint)> _inbound = new();

    private byte[] _gameUpdateBuffer =
    [
        MessageTypes.GAME_UPDATE
    ];

    public UdpGameServer(ILogger<UdpGameServer> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, PORT));
        _logger.LogInformation("UDP Game Server listening on port {Port}", PORT);
        
        var receiveLoopTask = RunReceiveLoopAsync(udpClient, stoppingToken);
        
        var sw = Stopwatch.StartNew();
        var nextTick = sw.Elapsed + TickInterval;
        long frame = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int catchups = 0;
                bool didTick = false;
                
                while (sw.Elapsed >= nextTick && catchups < MAX_CATCH_UPS_PER_LOOP)
                {
                    await HandleFastTick(udpClient);
                    frame++;
                    didTick = true;

                    if (frame % SLOW_TICK_EVERY_FRAMES == 0)
                    {
                        await HandleSlowTick(udpClient);
                    }

                    nextTick += TickInterval;
                    catchups++;
                }

                if (!didTick)
                {
                    var delay = nextTick - sw.Elapsed;
                    if (delay > TimeSpan.Zero)
                    {
                        try
                        {
                            await Task.Delay(delay, stoppingToken);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
        }
        finally
        {
            _logger.LogInformation("UDP Game Server is stopping.");
            try { await receiveLoopTask; } catch { /* ignore on shutdown */ }
        }
    }

    private async Task RunReceiveLoopAsync(UdpClient udpClient, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udpClient.ReceiveAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving UDP packet");
                continue;
            }

            _inbound.Enqueue((result.Buffer, result.RemoteEndPoint));
        }
    }

    private void HandleReceivedPacket(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        if (buffer.Length == 0) return;

        byte packetType = buffer[0];

        switch (packetType)
        {
            case MessageTypes.JOIN_GAME:
                if (_player1 == null)
                {
                    _player1 = new PlayerState(remoteEndPoint);
                    _logger.LogInformation("Player 1 joined from {Player1}", _player1.Ep);
                }
                else if (_player2 == null && !remoteEndPoint.Equals(_player1.Ep))
                {
                    _player2 = new PlayerState(remoteEndPoint);
                    _logger.LogInformation("Player 2 joined from {Player2}", _player2.Ep);

                    _gameState = GameState.WaitingForPlayersToBeReady;
                    _logger.LogInformation("Game state is now {GameState}", _gameState);
                }
                break;

            case MessageTypes.PLAYER_READY:
                if (_player1 != null && remoteEndPoint.Equals(_player1.Ep))
                {
                    _player1.Ready = true;
                    _logger.LogInformation("Player 1 is ready");
                }
                else if (_player2 != null && remoteEndPoint.Equals(_player2.Ep))
                {
                    _player2.Ready = true;
                    _logger.LogInformation("Player 2 is ready");
                }
                break;

            // TODO: Game update packets
        }
    }

    private async Task HandleSlowTick(UdpClient udpClient)
    {
        if (_gameState == GameState.WaitingForPlayersToBeReady)
        {
            if (_player1 is { Ready: false })
            {
                await udpClient.SendAsync(
                    ConstantMessages.PLAYER_READY,
                    ConstantMessages.PLAYER_READY.Length,
                    _player1.Ep);
            }

            if (_player2 is { Ready: false })
            {
                await udpClient.SendAsync(
                    ConstantMessages.PLAYER_READY,
                    ConstantMessages.PLAYER_READY.Length,
                    _player2.Ep);
            }

            if (_player1 is { Ready: true } && _player2 is { Ready: true })
            {
                _gameState = GameState.InProgress;
                _logger.LogInformation("Both players are ready. Game state is now {GameState}", _gameState);
            }
        }
    }

    private async Task HandleFastTick(UdpClient udpClient)
    {
        while (_inbound.TryDequeue(out var item))
        {
            HandleReceivedPacket(item.Buffer, item.RemoteEndPoint);
        }

        if (_gameState != GameState.InProgress)
        {
            return;
        }
        
        await Task.WhenAll(
            udpClient.SendAsync(_gameUpdateBuffer, _gameUpdateBuffer.Length, _player1?.Ep),
            udpClient.SendAsync(_gameUpdateBuffer, _gameUpdateBuffer.Length, _player2?.Ep)
        );
    }
}
