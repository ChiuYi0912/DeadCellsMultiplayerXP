namespace DeadCellsMultiplayerX.Server.Session.Connection;

using dc;
using dc.en;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Contracts.Protocol;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Serialization;
using DeadCellsMultiplayerX.Foundation.Versioning;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Server.Core;
using ModCore.Utilities;

internal sealed partial class ServerGuestConnection : IServerRPC
{
    /// <summary>
    /// 协议握手；版本不匹配时在进入游戏逻辑前拒绝连接。
    /// </summary>
    public Task<HelloResponse> Hello(HelloRequest request)
    {
        var response = new HelloResponse
        {
            ServerMajor = ProtocolVersion.Major,
            ServerMinor = ProtocolVersion.Minor,
            Capabilities = ProtocolVersion.SupportedCapabilities,
            Epoch = this.Session.Broadcaster.Epoch,
        };

        if (request == null)
        {
            response.Reason = "missing hello request";
            return Task.FromResult(response);
        }

        if (request.MAJOR != ProtocolVersion.Major)
        {
            response.Reason =
                $"protocol major mismatch: client={request.MAJOR} " +
                $"server={ProtocolVersion.Major}";
            this.Logger.Warning(
                "Protocol rejected: guid={Guid} {Reason}",
                this.GuestInfo.Guid, response.Reason);
            return Task.FromResult(response);
        }

        if (request.MINOR > ProtocolVersion.Minor)
        {
            response.Reason =
                $"protocol minor too new: client={request.MINOR} " +
                $"server={ProtocolVersion.Minor}";
            this.Logger.Warning(
                "Protocol rejected: guid={Guid} {Reason}",
                this.GuestInfo.Guid, response.Reason);
            return Task.FromResult(response);
        }

        if ((request.Capabilities & ProtocolVersion.RequiredCapabilities)
            != ProtocolVersion.RequiredCapabilities)
        {
            response.Reason =
                $"missing capabilities: client=0x{request.Capabilities:X} " +
                $"required=0x{ProtocolVersion.RequiredCapabilities:X}";
            this.Logger.Warning(
                "Protocol rejected: guid={Guid} {Reason}",
                this.GuestInfo.Guid, response.Reason);
            return Task.FromResult(response);
        }

        response.Accepted = true;

        this.Logger.Information(
            "Protocol accepted: guid={Guid} mod={ModVersion} " +
            "client={ClientMajor}.{ClientMinor} server={ServerMajor}.{ServerMinor} " +
            "caps=0x{Capabilities:X}",
            this.GuestInfo.Guid, request.ModVersion,
            request.MAJOR, request.MINOR,
            ProtocolVersion.Major, ProtocolVersion.Minor,
            request.Capabilities);

        return Task.FromResult(response);
    }

    public Task<bool> CheckVersion(string version)
    {
        if (Version.Parse(version) != VersionUtils.ModVersion)
        {
            this.Logger.Error("Dismatch version: {ver}", this.GuestInfo.Guid, version);
            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
            {
                if (this.rpc == null)
                {
                    return;
                }
                if (!this.rpc.IsDisposed)
                {
                    this.Dispose();
                }
            });
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    public Task SetGuestInfo(GuestInfo info)
    {
        this.GuestInfo = info;
        return Task.CompletedTask;
    }

    public async Task UploadSavedata(byte[] data)
    {
        await Task.Delay(1).ConfigureAwait(false);

        var savePath = ServerMain.Instance.SavePath;

        await File.WriteAllBytesAsync(savePath, data);

        this.Logger.Information("Saving savedata to {path}", savePath);

        this.Logger.Information("Building game...");

        await this.Main.LaunchGame();

        await Task.Delay(1).ConfigureAwait(false);
    }

    public async Task<byte[]> DownloadSavedata()
    {
        while (string.IsNullOrEmpty(this.Main.SavePath))
        {
            await Task.Yield();
        }
        return File.ReadAllBytes(this.Main.SavePath);
    }

    public Task<byte[]> RequestAreaInfo(IServerRPC.AreaInfoRequest request)
    {
        var rect = request.Rect;
        if (rect.X < 0)
        {
            rect.X = 0;
        }
        if (rect.Y < 0)
        {
            rect.Y = 0;
        }
        var areaInfo = new AreaInfo()
        {
            Rect = rect
        };

        var lvl = (dc.pr.Level)dc.pr.Game.Class.ME.subLevels.getDyn(request.SubLevelId);
        var map = lvl.map;

        areaInfo.Collision = new int[rect.Width * rect.Height];

        // 碰撞箱
        unsafe
        {
            var src = new Span<int>((void*)map.collisions.bytes, map.collisions.length);
            for (var y = 0; y < rect.Height; y++)
            {
                var dst = new Span<int>(areaInfo.Collision, y * rect.Width, rect.Width);
                src.Slice(((rect.Y + y) * map.wid) + rect.X, rect.Width).CopyTo(dst);
            }
        }

        // Entity
        {
            var rx = rect.X;
            var ry = rect.Y;
            var rxt = rect.X + rect.Width;
            var ryt = rect.Y + rect.Height;

            foreach (Entity v in lvl.entities)
            {
                if (v is not Mob)
                {
                    continue;
                }

                if (v.cx >= rx && v.cx <= rxt && v.cy >= ry && v.cy <= ryt && v.visible)
                {
                    // EntityInfo inf = worldXDataDirector.GetEntityByPointer(v.HashlinkPointer)!;

                    // if (inf != null)
                    // {
                    //     v.isOnScreen = true;

                    //     FillEntityInfo(v, inf);

                    //     areaInfo.Entities.Add(inf);
                    // }
                }
            }
        }

        // foreach (var item in await Session.GetGuestsHeroInfos())
        // {
        //     item.remoteTime = Session.CurrentTimeStamp;
        //     areaInfo.Entities.Add(item);
        // }

        return Task.FromResult(DCMXSerializers.MessagePack.Serialize(areaInfo));
    }


    public Task<long> GetTimeStamp() => Task.FromResult(this.Session.CurrentTimeStamp);


    public async Task GusetEnterNextLevel(string plyerid, string levelid)
    {
        if (string.IsNullOrWhiteSpace(plyerid)
            || string.IsNullOrWhiteSpace(levelid)
            || !this.Session.GuestsGameInfo.TryGetValue(plyerid, out var vote))
        {
            this.Logger.Warning(
                "Level transition request rejected: guid={Guid} level={Level}",
                plyerid, levelid);
            return;
        }

        if (vote.LevelSelected)
        {
            return;
        }

        vote.NextLevelID = levelid;
        vote.LevelSelected = true;

        if (!this.Session.TryBeginLevelTransition())
        {
            return;
        }

        // The gate now covers the vote wait as well: every exception path
        // must release it, otherwise all future transitions are locked.
        try
        {
            var deadline = Environment.TickCount64 + 10_000;
            while (!this.Session.GuestsGameInfo.All(e => e.Value.LevelSelected))
            {
                if (Environment.TickCount64 >= deadline)
                {
                    this.Logger.Warning(
                        "Level transition aborted: vote timeout after {TimeoutMs}ms",
                        10_000);
                    this.ResetLevelSelection();
                    return;
                }

                await Task.Delay(100);
            }

            var selectedLevelIds = this.Session.GuestsGameInfo
                .Select(e => e.Value.NextLevelID)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            if (selectedLevelIds.Count == 0)
            {
                this.Logger.Warning("Level transition aborted: no selected level id");
                this.ResetLevelSelection();
                return;
            }

            var resultLevelId = selectedLevelIds.Count == 1
                ? selectedLevelIds[0]
                : selectedLevelIds[Random.Shared.Next(selectedLevelIds.Count)];

            // The server world must follow the guests.  The client path
            // (IGuestRPC.EnterNextLevel) runs its own LevelTransition.goto;
            // the server deliberately does not re-send the initial save file.
            try
            {
                await this.Main.EnterNextLevel(resultLevelId);
            }
            catch (Exception ex)
            {
                this.Logger.Error(
                    ex,
                    "Server level transition failed: level={Level}; resetting votes",
                    resultLevelId);
                this.ResetLevelSelection();
                return;
            }

            // The server game has loaded the new level.  Start the new world
            // epoch before telling guests to run their local transition, so
            // frames from the old level cannot be mistaken for the new one.
            this.Session.BeginLevelEpoch();
            await this.Session.NoticeGuestsEnterNextLevel(resultLevelId);
        }
        finally
        {
            this.Session.EndLevelTransition();
        }
    }

    private void ResetLevelSelection()
    {
        foreach (var vote in this.Session.GuestsGameInfo.Values)
        {
            vote.LevelSelected = false;
        }
    }

    public Task SubmitLevelVote(LevelVote info)
    {
        this.Session.GuestsGameInfo[info.PlyerID] = info;
        return Task.CompletedTask;
    }

    public Task Ping() => Task.CompletedTask;

    public Task UploadClientWorldFrame(ClientWorldFrame frame)
    {
        this.Session.ClientFrames.OnFrame(this.GuestInfo.Guid, frame);
        return Task.CompletedTask;
    }

    public Task<WorldFrameBundle> RequestWorldFrames(WorldFrameRangeRequest request) => Task.FromResult(this.Session.Broadcaster.GetFrames(request, this.Session.CurrentTimeStamp));

    public Task<WorldResync> RequestWorldResync(int knownEpoch) => Task.FromResult(this.Session.Broadcaster.BuildResync(this.Session.CurrentTimeStamp));

    public Task SubmitHeroAction(HeroAction request)
    {
        this.Session.HandleHeroAction(this.GuestInfo.Guid, request);
        return Task.CompletedTask;
    }
}
