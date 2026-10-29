using System;
using dc.en;
using dc.tool;
using dc.tool.hero;
using dc.tool.hero.weap;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using ModCore.Utilities;
using Serilog;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;

namespace DeadCellsMultiplayerX.Client.Heroes.LocalHero
{
    /// <summary>
    /// hook本地客户端的武器与技能触发
    /// </summary>
    internal sealed class LocalGhostCapture
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "LocalGhostCapture");

        private readonly GuestClientSession session;

        /// <summary>
        /// 用于发送事件时的唯一id
        /// </summary>
        private long nextActionId;

        public LocalGhostCapture(GuestClientSession session)
        {
            this.session = session;
        }

        public void Init()
        {
            Hook_HeroActiveSkillsManager.onActiveSkill += OnActiveSkill;
            Hook_BeheadedWeaponsManager.onWeaponExecute += OnWeaponExecute;
            Hook_Inventory.add += OnInventoryAdd;
        }

        public void Dispose()
        {
            Hook_HeroActiveSkillsManager.onActiveSkill -= OnActiveSkill;
            Hook_BeheadedWeaponsManager.onWeaponExecute -= OnWeaponExecute;
            Hook_Inventory.add -= OnInventoryAdd;
        }


        /// <summary>
        /// 上报本地释放的主动技能
        /// </summary>
        private void OnActiveSkill(
            Hook_HeroActiveSkillsManager.orig_onActiveSkill orig,
            HeroActiveSkillsManager self,
            int id,
            double ratio)
        {
            var game = session.Game;
            var hero = self.hero;
            bool isLocal = hero != null && ReferenceEquals(hero, game?.hero);

            int cx = 0, cy = 0, dir = 0;
            double xr = 0, yr = 0, dx = 0, dy = 0;

            if (isLocal)
            {
                cx = hero!.cx;
                cy = hero.cy;
                xr = hero.xr;
                yr = hero.yr;
                dir = hero.dir;
                dx = hero.dx;
                dy = hero.dy;
            }

            orig(self!, id, ratio);

            if (!isLocal) return;

            var item = hero?.inventory.getActiveOn(id);
            if (item == null) return;

            SendHeroAction(
                hero!, HeroActionKind.ActiveSkill, id, ItemCodec.Collect(item),
                ratio, 0, cx, cy, xr, yr, dir, dx, dy);
        }

        /// <summary>
        /// 武器命中帧
        /// </summary>
        private void OnWeaponExecute(
            Hook_BeheadedWeaponsManager.orig_onWeaponExecute orig,
            BeheadedWeaponsManager self,
            dc.tool.Weapon weapon)
        {
            var game = session.Game;
            var hero = self?.hero;
            bool isLocal = hero != null && ReferenceEquals(hero, game?.hero);
            var item = isLocal ? weapon?.item : null;
            int slot = item?.posID ?? -1;

            // 背包武器等非主武器槽位的执行不上报
            if (!isLocal || item == null || slot is < 0 or > 1)
            {
                orig(self!, weapon);
                return;
            }

            int cx = hero!.cx;
            int cy = hero.cy;
            double xr = hero.xr;
            double yr = hero.yr;
            int dir = hero.dir;
            double dx = hero.dx;
            double dy = hero.dy;
            int cycle = weapon!.get_cycle();

            int epoch = session.RemoteWorld?.Frames.Epoch ?? -1;
            long token = epoch >= 0 ? session.Prediction.BeginWeapon() : 0;

            try
            {
                orig(self!, weapon);
            }
            finally
            {
                if (token != 0)
                    session.Prediction.EndWeapon();
            }

            // ActionId 和 epoch 在 orig 之后才确定
            int sendEpoch = session.RemoteWorld?.Frames.Epoch ?? -1;
            long actionId = 0;
            if (sendEpoch >= 0)
            {
                actionId = ++nextActionId;

                if (token != 0)
                    session.Prediction.CommitWeapon(token, actionId);
            }

            SendHeroAction(
                hero, HeroActionKind.Weapon, slot,
                ItemCodec.Collect(item),
                1.0, cycle, cx, cy, xr, yr, dir, dx, dy,
                sendEpoch, actionId);
        }

        /// <summary>
        /// 上报本地切换后的项链
        /// </summary>
        private InventItem OnInventoryAdd(Hook_Inventory.orig_add orig, Inventory self, InventItem item)
        {
            var game = session.Game;
            var hero = game?.hero;

            bool isLocal = hero != null && ReferenceEquals(self, hero.inventory);

            bool isTalisman = isLocal && item?.kind is InventItemKind.Talisman;

            var result = orig(self, item);

            if (!isTalisman) return result;

            var talisman = hero!.inventory.hasTalisman()
                ? hero.inventory.getTalisman()
                : item;

            if (talisman == null) return result;

            SendHeroAction(
                hero, HeroActionKind.Talisman, 0,
                ItemCodec.Collect(talisman),
                1.0, 0,
                hero.cx, hero.cy, hero.xr, hero.yr,
                hero.dir, hero.dx, hero.dy);

            return result;
        }


        /// <summary>
        /// 发送事件给服务端
        /// </summary>
        private void SendHeroAction(
            Hero hero,
            HeroActionKind kind,
            int slot,
            ItemRef item,
            double ratio,
            int cycle,
            int cx, int cy,
            double xr, double yr,
            int dir,
            double dx, double dy,
            int? epoch = null,
            long? actionId = null)
        {
            var world = session.RemoteWorld;
            if (world == null) return;

            int currentEpoch = epoch ?? world.Frames.Epoch;
            if (currentEpoch < 0) return;

            var request = new HeroAction
            {
                Epoch = currentEpoch,
                ActionId = actionId ?? ++nextActionId,
                Kind = kind,
                Slot = slot,
                Item = item,
                Ratio = ratio,
                Cycle = cycle,
                CX = cx,
                CY = cy,
                XR = xr,
                YR = yr,
                Dir = dir,
                DX = dx,
                DY = dy,
                ClientTimeMs = session.SyncedTimeMs,
                LevelId = hero._level?.map?.id?.ToString() ?? string.Empty,
                SubLevelIndex = hero._level?.GetSubLevelIndex() ?? -1,
                BrutalityTier = hero.brutalityTier,
                SurvivalTier = hero.survivalTier,
                TacticTier = hero.tacticTier,
            };

            var task = session.Server.SubmitHeroAction(request);
            _ = task.ContinueWith(
                static t => _ = t.Exception,
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// DEBUG:直接向服务端上报指定技能的释放
        /// </summary>
        internal void DebugCastSkill(string itemId, int slot)
        {
            var hero = session.Game?.hero;
            var world = session.RemoteWorld;
            if (hero == null || world == null) return;
            if (world.Frames.Epoch < 0) return;

            SendHeroAction(
                hero, HeroActionKind.ActiveSkill, slot,
                new ItemRef
                {
                    Kind = (int)dc.tool.InventItemKind.Indexes.Active,
                    ItemId = itemId,
                    ItemLevel = 1,
                    PosID = slot,
                },
                1.0, 0,
                hero.cx, hero.cy, hero.xr, hero.yr, hero.dir, hero.dx, hero.dy);

            Logger.Information("Debug skill cast sent: item={Item} slot={Slot}", itemId, slot);
        }

        /// <summary>
        /// DEBUG:直接向服务端上报指定武器的使用
        /// </summary>
        internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
        {
            var hero = session.Game?.hero;
            var world = session.RemoteWorld;
            if (hero == null || world == null) return;
            if (world.Frames.Epoch < 0) return;

            SendHeroAction(
                hero, HeroActionKind.Weapon, slot,
                new ItemRef
                {
                    Kind = (int)dc.tool.InventItemKind.Indexes.Weapon,
                    ItemId = itemId,
                    ItemLevel = 1,
                    PosID = slot,
                },
                1.0, cycle,
                hero.cx, hero.cy, hero.xr, hero.yr, hero.dir, hero.dx, hero.dy);

            Logger.Information(
                "Debug weapon use sent: item={Item} slot={Slot} cycle={Cycle}",
                itemId, slot, cycle);
        }
    }
}
