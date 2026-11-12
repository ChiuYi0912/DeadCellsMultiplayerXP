namespace DeadCellsMultiplayerX.Networking.Time;

using System.Diagnostics;

internal sealed class ServerTimeSystem
{
    private readonly Stopwatch stopwatch = new();
    private long prevMs;    //上一个时间戳
    public long Now { get; private set; }   //当前服务端时间

    public void Start()
    {
        this.stopwatch.Restart();
        this.prevMs = 0;
        this.Now = 0;
    }

    //每帧调用
    public void Tick()
    {
        var now = this.stopwatch.ElapsedMilliseconds;
        this.Now += now - this.prevMs;
        this.prevMs = now;
    }
}
