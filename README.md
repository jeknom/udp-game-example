# UDP Game Example (WIP)

This is an example of how to setup an aspnet game server that utilizes UDP protocol to communicate with clients. It also includes a game made using Raylib. In the game, two players connect to a game and compete on who is faster in clicking on randomly appearing shapes.

## How it works (happy path)

```mermaid
sequenceDiagram
    autonumber
    box Server
        participant Program as Program.cs
        participant Exec as UdpGameServer.ExecuteAsync
        participant Handler as UdpGameServer.HandleReceivedPacket
        participant RxLoop as UdpGameServer.RunReceiveLoopAsync
    end
    
    box Clients
        participant CA as Client A
        participant CB as Client B
    end

    note over Program:          Start server
    Program->>Exec:             Register UdpGameServer as background process
    Exec-->>RxLoop:             Start receive loop on another thread

    
    note over CA: Show "connecting..."
    CA-->>RxLoop: JOIN_GAME
    RxLoop-->>Handler: Queue received packet
    activate Handler
    note over Handler: Process JOIN_GAME
    note over Handler: Save Client A IP as Player 1
    deactivate Handler

    CB-->>RxLoop: JOIN_GAME
    RxLoop-->>Handler: Queue received packet
    activate Handler
    note over Handler: Process JOIN_GAME
    note over Handler: Save Client B IP as Player 2
    note over Handler: Change game state to WaitingForPlayersToBeReady
    deactivate Handler

    loop Every second
        par To Client A
            Handler-->>CA: Ask if client is ready
        and To Client B
            Handler-->>CB: Ask if client is ready
        end
    end
    
    note over CA: Transition to game scene
    note over CB: Transition to game scene
    CA-->>RxLoop: PLAYER_READY
    CB-->>RxLoop: PLAYER_READY
    RxLoop-->>Handler: Queue received packets
    Handler-->>Exec: Mark players as ready
    note over Exec: Change game state to InProgress

    loop Every tick (60 per second)
        par To Client A
            Exec-->>CA: Send game update packet
        and To Client B
            Exec-->>CB: Send game update packet
        end
    end
```