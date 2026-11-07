using System;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Utils;

namespace DeadCellsMultiplayerX.Client.UI.DebugTools
{
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
            string version = VersionUtils.ModVersion.ToString(3);

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

            var main = ClientMain.Instance;
            var guest = main.CurrentGuestClient;
            var lobby = guest?.LobbyInfo;

            int players = lobby?.Guests?.Count ?? 1;
            string role = main.CurrentHostClient != null ? "Host" : "Guest";
            string header = $"DCMX Version:{version} Role:{role} Players Count:{players}";

            var world = session.RemoteWorld;
            string guid = guest?.Guid ?? "--";
            string shortGuid = guid.Length > 8 ? guid[..8] : guid;

            string level = "--";
            string hero = "none";

            if (world != null)
            {
                var ctx = world.LocalHero.Context;
                if (!string.IsNullOrEmpty(ctx.CurrentLevelId))
                {
                    level = ctx.SubLevelIndex >= 0
                        ? $"{ctx.CurrentLevelId}#{ctx.SubLevelIndex}"
                        : ctx.CurrentLevelId;
                }

                hero = !ctx.HasHero ? "none" : ctx.IsAlive ? "alive" : "dead";
            }

            string identity = $"PlayersID:{shortGuid} Level:{level} HeroStats={hero}";

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

            var frames = world.Frames;
            long ageMs = frames.LastFrameAppliedAtMs < 0
                ? -1
                : Environment.TickCount64 - frames.LastFrameAppliedAtMs;

            int hz = ageMs > 1000
                ? 0
                : (int)Math.Round(frames.AppliedFramesPerSecond);

            if (!frames.HasBaseline)
                identity += " Base=Wait";

            if (session.UploadBroken)
                identity += " | Up=Broken";

            int mobs = 0;
            foreach (var mob in world.Mobs.Receiver.All)
                if (!mob.destroyed) mobs++;

            int heroes = 0;
            foreach (var _ in world.Heros.All)
                heroes++;

            int entities = 0;
            int pendingSpawns = 0;
            foreach (var record in world.EntityStore.Records)
            {
                entities++;
                if (!record.Created) pendingSpawns++;
            }

            return new ClientNetSnapshot(
                HasSession: true,
                HasWorld: true,
                Header: header,
                Identity: identity,
                Players: players,
                Epoch: frames.Epoch,
                Sequence: frames.LastDeliveredSequence,
                Hz: hz,
                AgeMs: ageMs,
                FrameBytes: session.LastWorldFrameBytes,
                HasBaseline: frames.HasBaseline,
                Pending: frames.PendingCount,
                BufferedFrames: frames.BufferedFrames,
                Gap: frames.GapCount,
                Nack: frames.NackSent,
                Bundle: frames.BundleReceived,
                Resync: frames.ResyncCount,
                Skip: frames.SkippedFrames,
                Drop: frames.DroppedOld,
                Overflow: frames.PendingOverflow,
                RttMs: session.ServerRttMs,
                OffsetMs: session.ServerClockOffsetMs,
                Mobs: mobs,
                Ghosts: world.Ghosts.Count,
                Heroes: heroes,
                Entities: entities,
                PendingSpawns: pendingSpawns,
                Pred: session.Prediction.PendingMobCount,
                UploadBroken: session.UploadBroken);
        }
    }
}
