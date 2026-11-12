namespace DeadCellsMultiplayerX.Client.UI.DebugTools.Stats;

using DeadCellsMultiplayerX.Client.Session;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World;


using DeadCellsMultiplayerX.Foundation.Versioning;

internal readonly record struct ClientNetSnapshot(
    bool HasSession,
    bool HasWorld,
    string Header,
    string Identity,
    int Players,
    long Epoch,
    long Sequence,
    int Hz,
    long AgeMs,
    int FrameBytes,
    bool HasBaseline,
    int Pending,
    int BufferedFrames,
    int Gap,
    int Nack,
    int Bundle,
    int Resync,
    int Skip,
    int Drop,
    int Overflow,
    long RttMs,
    long OffsetMs,
    int Mobs,
    int Ghosts,
    int Heroes,
    int Entities,
    int PendingSpawns,
    int Pred,
    bool UploadBroken);

internal static class ClientNetStats
{
    public static ClientNetSnapshot Collect(GuestClientSession? session)
    {
        var version = VersionUtils.ModVersion.ToString(3);

        if (session == null || session.IsDisposed)
        {
            return new ClientNetSnapshot(
                HasSession: false,
                HasWorld: false,
                Header: $"DCMX version:{version} role:idle",
                Identity: "waiting for session",
                Players: 1,
                Epoch: -1,
                Sequence: -1,
                Hz: 0,
                AgeMs: -1,
                FrameBytes: 0,
                HasBaseline: false,
                Pending: 0,
                BufferedFrames: 0,
                Gap: 0,
                Nack: 0,
                Bundle: 0,
                Resync: 0,
                Skip: 0,
                Drop: 0,
                Overflow: 0,
                RttMs: -1,
                OffsetMs: 0,
                Mobs: 0,
                Ghosts: 0,
                Heroes: 0,
                Entities: 0,
                PendingSpawns: 0,
                Pred: 0,
                UploadBroken: false);
        }

        var main = ClientRuntime.Instance;
        var guest = main.CurrentGuestClient;
        var lobby = guest?.LobbyInfo;

        var players = lobby?.Guests?.Count ?? 1;
        var role = main.CurrentHostClient != null ? "Host" : "Guest";
        var header = $"DCMX Version:{version} Role:{role} Players Count:{players}";

        var world = session.ClientWorld;
        var guid = guest?.Guid ?? "--";
        var shortGuid = guid.Length > 8 ? guid[..8] : guid;

        var level = "--";
        var hero = "none";
        ClientWorldStats stats = default;

        if (world != null)
        {
            stats = world.CaptureStats();
            if (!string.IsNullOrEmpty(stats.LevelId))
            {
                level = stats.SubLevelIndex >= 0
                    ? $"{stats.LevelId}#{stats.SubLevelIndex}"
                    : stats.LevelId;
            }

            hero = !stats.HasHero ? "none" : stats.IsAlive ? "alive" : "dead";
        }

        var identity = $"PlayersID:{shortGuid} Level:{level} HeroStats={hero}";

        if (world == null)
        {
            return new ClientNetSnapshot(
                HasSession: true,
                HasWorld: false,
                Header: header,
                Identity: identity,
                Players: players,
                Epoch: -1,
                Sequence: -1,
                Hz: 0,
                AgeMs: -1,
                FrameBytes: 0,
                HasBaseline: false,
                Pending: 0,
                BufferedFrames: 0,
                Gap: 0,
                Nack: 0,
                Bundle: 0,
                Resync: 0,
                Skip: 0,
                Drop: 0,
                Overflow: 0,
                RttMs: -1,
                OffsetMs: 0,
                Mobs: 0,
                Ghosts: 0,
                Heroes: 0,
                Entities: 0,
                PendingSpawns: 0,
                Pred: 0,
                UploadBroken: session.UploadBroken);
        }

        var ageMs = stats.LastFrameAppliedAtMs < 0
            ? -1
            : Environment.TickCount64 - stats.LastFrameAppliedAtMs;

        var hz = ageMs > 1000
            ? 0
            : (int)Math.Round(stats.AppliedFramesPerSecond);

        if (!stats.HasBaseline)
        {
            identity += " Base=Wait";
        }

        if (session.UploadBroken)
        {
            identity += " | Up=Broken";
        }

        return new ClientNetSnapshot(
            HasSession: true,
            HasWorld: true,
            Header: header,
            Identity: identity,
            Players: players,
            Epoch: stats.Epoch,
            Sequence: stats.LastDeliveredSequence,
            Hz: hz,
            AgeMs: ageMs,
            FrameBytes: session.LastWorldFrameBytes,
            HasBaseline: stats.HasBaseline,
            Pending: stats.PendingCount,
            BufferedFrames: stats.BufferedFrames,
            Gap: stats.GapCount,
            Nack: stats.NackSent,
            Bundle: stats.BundleReceived,
            Resync: stats.ResyncCount,
            Skip: stats.SkippedFrames,
            Drop: stats.DroppedOld,
            Overflow: stats.PendingOverflow,
            RttMs: session.ServerRttMs,
            OffsetMs: session.ServerClockOffsetMs,
            Mobs: stats.Mobs,
            Ghosts: stats.Ghosts,
            Heroes: stats.Heroes,
            Entities: stats.Entities,
            PendingSpawns: stats.PendingSpawns,
            Pred: session.Prediction.PendingMobCount,
            UploadBroken: session.UploadBroken);
    }
}
