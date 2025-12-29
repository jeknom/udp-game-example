using System.Net;

namespace shared;

public class PlayerState
{
    public IPEndPoint Ep { get; init; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool Ready { get; set; }
    public int Score { get; set; }

    public PlayerState(IPEndPoint ep)
    {
        Ep = ep;
        X = 0;
        Y = 0;
        Ready = false;
        Score = 0;
    }
}