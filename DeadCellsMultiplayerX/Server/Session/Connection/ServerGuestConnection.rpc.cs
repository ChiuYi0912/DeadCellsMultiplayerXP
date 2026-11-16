namespace DeadCellsMultiplayerX.Server.Session.Connection;

using dc;
using dc.en;
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
                this.ConnectionGuid, response.Reason);
            return Task.FromResult(response);
        }

        if (request.MINOR > ProtocolVersion.Minor)
        {
            response.Reason =
                $"protocol minor too new: client={request.MINOR} " +
                $"server={ProtocolVersion.Minor}";
            this.Logger.Warning(
                "Protocol rejected: guid={Guid} {Reason}",
                this.ConnectionGuid, response.Reason);
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
                this.ConnectionGuid, response.Reason);
            return Task.FromResult(response);
        }

        response.Accepted = true;

        this.Logger.Information(
            "Protocol accepted: guid={Guid} mod={ModVersion} " +
            "client={ClientMajor}.{ClientMinor} server={ServerMajor}.{ServerMinor} " +
            "caps=0x{Capabilities:X}",
            this.ConnectionGuid, request.ModVersion,
            request.MAJOR, request.MINOR,
            ProtocolVersion.Major, ProtocolVersion.Minor,
            request.Capabilities);

        return Task.FromResult(response);
    }

    public Task<bool> CheckVersion(string version)
    {
        if (Version.Parse(version) != VersionUtils.ModVersion)
        {
            this.Logger.Error(
                "Dismatch version: guid={Guid} ver={Ver}",
                this.ConnectionGuid, version);
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
        if (info == null)
        {
            return Task.CompletedTask;
        }

        // 连接身份来自握手，不能被后续客户端消息改写；
        // 否则帧/动作会归属到另一个 ServerHero。
        if (!string.Equals(info.Guid, this.ConnectionGuid, StringComparison.Ordinal))
        {
            this.Logger.Warning(
                "Guest info GUID mismatch: connection={Connection} reported={Reported}; " +
                "keeping connection GUID",
                this.ConnectionGuid, info.Guid);
            info.Guid = this.ConnectionGuid;
        }

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


    public Task GusetEnterNextLevel(string plyerid, string levelid)
        => this.Session.LevelTransitions
            .GusetEnterNextLevelAsync(this.ConnectionGuid, plyerid, levelid)
            .AsTask();

    public Task SubmitLevelVote(LevelVote info)
        => this.Session.LevelTransitions
            .SubmitLevelVoteAsync(this.ConnectionGuid, info)
            .AsTask();

    public Task Ping() => Task.CompletedTask;

    public Task UploadClientWorldFrame(ClientWorldFrame frame)
    {
        this.Session.ClientFrames.OnFrame(this.ConnectionGuid, frame);
        return Task.CompletedTask;
    }

    public Task<WorldFrameBundle> RequestWorldFrames(WorldFrameRangeRequest request) => Task.FromResult(this.Session.Broadcaster.GetFrames(request, this.Session.CurrentTimeStamp));

    public Task<WorldResync> RequestWorldResync(int knownEpoch) => Task.FromResult(this.Session.Broadcaster.BuildResync(this.Session.CurrentTimeStamp));

}
