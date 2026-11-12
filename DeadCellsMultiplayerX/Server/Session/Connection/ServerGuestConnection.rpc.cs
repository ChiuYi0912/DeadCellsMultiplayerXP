using dc;
using dc.en;
using DeadCellsMultiplayerX.Contracts.Protocol;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Engine.Serialization;
using ModCore.Utilities;
using DeadCellsMultiplayerX.Server.Core;

using DeadCellsMultiplayerX.Contracts.Lobby;

using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Foundation.Versioning;
namespace DeadCellsMultiplayerX.Server.Session.Connection
{
    internal partial class ServerGuestConnection : IServerRPC
    {
        /// <summary>
        /// 协议握手；版本不匹配时在进入游戏逻辑前拒绝连接。
        /// </summary>
        public Task<HelloResponse> Hello(HelloRequest request)
        {
            var response = new HelloResponse
            {
                ServerMajor = ProtocolVersion.MAJOR,
                ServerMinor = ProtocolVersion.MINOR,
                Capabilities = ProtocolVersion.SUPPORTED_CAPABILITIES,
                Epoch = Session.Broadcaster.Epoch,
            };

            if (request == null)
            {
                response.Reason = "missing hello request";
                return Task.FromResult(response);
            }

            if (request.MAJOR != ProtocolVersion.MAJOR)
            {
                response.Reason =
                    $"protocol major mismatch: client={request.MAJOR} " +
                    $"server={ProtocolVersion.MAJOR}";
                Logger.Warning(
                    "Protocol rejected: guid={Guid} {Reason}",
                    GuestInfo.Guid, response.Reason);
                return Task.FromResult(response);
            }

            if (request.MINOR > ProtocolVersion.MINOR)
            {
                response.Reason =
                    $"protocol minor too new: client={request.MINOR} " +
                    $"server={ProtocolVersion.MINOR}";
                Logger.Warning(
                    "Protocol rejected: guid={Guid} {Reason}",
                    GuestInfo.Guid, response.Reason);
                return Task.FromResult(response);
            }

            if ((request.Capabilities & ProtocolVersion.REQUIRED_CAPABILITIES)
                != ProtocolVersion.REQUIRED_CAPABILITIES)
            {
                response.Reason =
                    $"missing capabilities: client=0x{request.Capabilities:X} " +
                    $"required=0x{ProtocolVersion.REQUIRED_CAPABILITIES:X}";
                Logger.Warning(
                    "Protocol rejected: guid={Guid} {Reason}",
                    GuestInfo.Guid, response.Reason);
                return Task.FromResult(response);
            }

            response.Accepted = true;

            Logger.Information(
                "Protocol accepted: guid={Guid} mod={ModVersion} " +
                "client={ClientMajor}.{ClientMinor} server={ServerMajor}.{ServerMinor} " +
                "caps=0x{Capabilities:X}",
                GuestInfo.Guid, request.ModVersion,
                request.MAJOR, request.MINOR,
                ProtocolVersion.MAJOR, ProtocolVersion.MINOR,
                request.Capabilities);

            return Task.FromResult(response);
        }

        public Task<bool> CheckVersion(string version)
        {
            if (Version.Parse(version) != VersionUtils.ModVersion)
            {
                Logger.Error("Dismatch version: {ver}", guestInfo.Guid, version);
                Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                {
                    if (rpc == null)
                    {
                        return;
                    }
                    if (!rpc.IsDisposed)
                    {
                        Dispose();
                    }
                });
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }

        public Task SetGuestInfo(GuestInfo info)
        {
            GuestInfo = info;
            return Task.CompletedTask;
        }

        public async Task UploadSavedata(byte[] data)
        {
            await Task.Delay(1).ConfigureAwait(false);

            var savePath = ServerMain.Instance.savePath;

            await File.WriteAllBytesAsync(savePath, data);

            Logger.Information("Saving savedata to {path}", savePath);

            Logger.Information("Building game...");

            await Main.LaunchGame();

            await Task.Delay(1).ConfigureAwait(false);
        }

        public async Task<byte[]> DownloadSavedata()
        {
            while (string.IsNullOrEmpty(Main.savePath))
            {
                await Task.Yield();
            }
            return File.ReadAllBytes(Main.savePath);
        }

        public async Task<byte[]> RequestAreaInfo(IServerRPC.AreaInfoRequest request)
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
                for (int y = 0; y < rect.Height; y++)
                {
                    var dst = new Span<int>(areaInfo.Collision, y * rect.Width, rect.Width);
                    src.Slice((rect.Y + y) * map.wid + rect.X, rect.Width).CopyTo(dst);
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

            return DCMXSerializers.MessagePack.Serialize(areaInfo);
        }


        public Task<long> GetTimeStamp() => Task.FromResult(Session.CurrentTimeStamp);


        public async Task GusetEnterNextLevel(string plyerid, string levelid)
        {
            if (string.IsNullOrWhiteSpace(plyerid)
                || string.IsNullOrWhiteSpace(levelid)
                || !Session.GuestsGameInfo.TryGetValue(plyerid, out var vote))
            {
                Logger.Warning(
                    "Level transition request rejected: guid={Guid} level={Level}",
                    plyerid, levelid);
                return;
            }

            if (vote.LevelSelected)
                return;

            vote.NextLevelID = levelid;
            vote.LevelSelected = true;

            if (!Session.TryBeginLevelTransition())
                return;

            // The gate now covers the vote wait as well: every exception path
            // must release it, otherwise all future transitions are locked.
            try
            {
                long deadline = Environment.TickCount64 + 10_000;
                while (!Session.GuestsGameInfo.All(e => e.Value.LevelSelected))
                {
                    if (Environment.TickCount64 >= deadline)
                    {
                        Logger.Warning(
                            "Level transition aborted: vote timeout after {TimeoutMs}ms",
                            10_000);
                        ResetLevelSelection();
                        return;
                    }

                    await Task.Delay(100);
                }

                var selectedLevelIds = Session.GuestsGameInfo
                    .Select(e => e.Value.NextLevelID)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList();

                if (selectedLevelIds.Count == 0)
                {
                    Logger.Warning("Level transition aborted: no selected level id");
                    ResetLevelSelection();
                    return;
                }

                string resultLevelId = selectedLevelIds.Count == 1
                    ? selectedLevelIds[0]
                    : selectedLevelIds[System.Random.Shared.Next(selectedLevelIds.Count)];

                // The server world must follow the guests.  The client path
                // (IGuestRPC.EnterNextLevel) runs its own LevelTransition.goto;
                // the server deliberately does not re-send the initial save file.
                try
                {
                    await Main.EnterNextLevel(resultLevelId);
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        ex,
                        "Server level transition failed: level={Level}; resetting votes",
                        resultLevelId);
                    ResetLevelSelection();
                    return;
                }

                // The server game has loaded the new level.  Start the new world
                // epoch before telling guests to run their local transition, so
                // frames from the old level cannot be mistaken for the new one.
                Session.BeginLevelEpoch();
                await Session.NoticeGuestsEnterNextLevel(resultLevelId);
            }
            finally
            {
                Session.EndLevelTransition();
            }
        }

        private void ResetLevelSelection()
        {
            foreach (var vote in Session.GuestsGameInfo.Values)
                vote.LevelSelected = false;
        }

        public Task SubmitLevelVote(LevelVote info)
        {
            Session.GuestsGameInfo[info.PlyerID] = info;
            return Task.CompletedTask;
        }

        public Task Ping() => Task.CompletedTask;

        public Task UploadClientWorldFrame(ClientWorldFrame frame)
        {
            Session.ClientFrames.OnFrame(GuestInfo.Guid, frame);
            return Task.CompletedTask;
        }

        public Task<WorldFrameBundle> RequestWorldFrames(WorldFrameRangeRequest request)
        {
            return Task.FromResult(Session.Broadcaster.GetFrames(request, Session.CurrentTimeStamp));
        }

        public Task<WorldResync> RequestWorldResync(int knownEpoch)
        {
            return Task.FromResult(Session.Broadcaster.BuildResync(Session.CurrentTimeStamp));
        }

        public Task SubmitHeroAction(HeroAction request)
        {
            Session.HandleHeroAction(GuestInfo.Guid, request);
            return Task.CompletedTask;
        }
    }
}
