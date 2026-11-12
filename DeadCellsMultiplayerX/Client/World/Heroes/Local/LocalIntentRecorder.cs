using dc.en;
using dc.tool;
using dc.tool.atk;
using dc.tool.hero;
using dc.tool.hero.weap;
using dc.tool.weap;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Engine.Adapters.Items;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Engine.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Client.World.Heroes.Local
{
    /// <summary>
    /// 本地动作意图记录：从 Hook 捕获技能/武器/项链/盾牌，
    /// 构造 HeroAction 并交给发送通道。
    /// </summary>
    internal sealed class LocalIntentRecorder : IClientActionHookSink
    {
        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "LocalIntentRecorder");

        private readonly GuestClientSession session;
        private readonly LocalActionSender sender;
        private readonly LocalActionDeduper dedupe;

        public LocalIntentRecorder(
            GuestClientSession session,
            LocalActionSender sender,
            LocalActionDeduper dedupe)
        {
            this.session = session
                ?? throw new ArgumentNullException(nameof(session));
            this.sender = sender
                ?? throw new ArgumentNullException(nameof(sender));
            this.dedupe = dedupe
                ?? throw new ArgumentNullException(nameof(dedupe));
        }

        public void Clear() => dedupe.Clear();

        /// <summary>
        /// 上报本地释放的主动技能
        /// </summary>
        public void OnActiveSkill(
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
        public void OnWeaponExecute(
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

            int epoch = session.ClientWorld?.Epoch ?? -1;
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
            int sendEpoch = session.ClientWorld?.Epoch ?? -1;
            long actionId = 0;
            if (sendEpoch >= 0)
            {
                actionId = dedupe.NextActionId();

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
        public InventItem OnInventoryAdd(
            Hook_Inventory.orig_add orig,
            Inventory self,
            InventItem item)
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

        public void OnShieldChargeStart(
            Hook_BaseShield.orig_onShieldChargeStart orig, BaseShield self)
        {
            orig(self);
            if (!IsLocalShield(self)) return;

            if (!dedupe.TryBeginHold(self)) return;

            SendShieldAction(self, ShieldActionKind.HoldStart);
        }

        public void OnShieldReleased(
            Hook_BaseShield.orig_onShieldReleased orig, BaseShield self)
        {
            orig(self);
            if (!IsLocalShield(self)) return;

            dedupe.EndHold(self);
            SendShieldAction(self, ShieldActionKind.HoldEnd);
        }

        public void OnStartParry(
            Hook_BaseShield.orig_startParry orig, BaseShield self)
        {
            orig(self);
            if (!IsLocalShield(self)) return;

            if (!dedupe.TryBeginParry(self)) return;

            SendShieldAction(self, ShieldActionKind.ParryStart);
        }

        public void OnShieldEndParry(
            Hook_BaseShield.orig_onShieldEndParry orig, BaseShield self)
        {
            orig(self);
            if (!IsLocalShield(self)) return;

            if (!dedupe.TryEndParry(self)) return;

            SendShieldAction(self, ShieldActionKind.ParryEnd);
        }

        public void OnShieldBlock(
            Hook_BaseShield.orig_onShieldBlock orig,
            BaseShield self,
            AttackData sourceAtk,
            bool fullParry)
        {
            orig(self, sourceAtk, fullParry);
            if (!IsLocalShield(self)) return;

            SendShieldAction(
                self, ShieldActionKind.Block, sourceAtk, fullParry);
        }

        public void OnShieldCounterAttack(
            Hook_BaseShield.orig_shieldCounterAttack orig,
            BaseShield self,
            AttackData sourceAtk,
            bool fullParry)
        {
            orig(self, sourceAtk, fullParry);
            if (!IsLocalShield(self)) return;

            SendShieldAction(
                self, ShieldActionKind.Counter, sourceAtk, fullParry);
        }

        /// <summary>
        /// DEBUG:直接向服务端上报指定技能的释放
        /// </summary>
        internal void DebugCastSkill(string itemId, int slot)
        {
            var hero = session.Game?.hero;
            var world = session.ClientWorld;
            if (hero == null || world == null) return;
            if (world.Epoch < 0) return;

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

            Logger.Information(
                "Debug skill cast sent: item={Item} slot={Slot}", itemId, slot);
        }

        /// <summary>
        /// DEBUG:直接向服务端上报指定武器的使用
        /// </summary>
        internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
        {
            var hero = session.Game?.hero;
            var world = session.ClientWorld;
            if (hero == null || world == null) return;
            if (world.Epoch < 0) return;

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

        private bool IsLocalShield(BaseShield? shield)
        {
            if (shield == null || shield.destroyed)
                return false;

            var hero = session.Game?.hero;
            if (hero == null || !ReferenceEquals(shield.owner, hero))
                return false;

            return shield.item != null && shield.item.posID is >= 0 and <= 1;
        }

        private void SendShieldAction(
            BaseShield shield,
            ShieldActionKind kind,
            AttackData? sourceAtk = null,
            bool fullParry = false)
        {
            var hero = session.Game?.hero;
            var item = shield.item;
            if (hero == null || item == null)
                return;

            int slot = item.posID;
            if (slot is < 0 or > 1)
                return;

            SendHeroAction(
                hero, HeroActionKind.Shield, slot,
                ItemCodec.Collect(item),
                1.0, 0,
                hero.cx, hero.cy, hero.xr, hero.yr,
                hero.dir, hero.dx, hero.dy,
                shieldKind: kind,
                fullParry: fullParry,
                sourceNetId: ResolveSourceNetId(sourceAtk));
        }

        private uint ResolveSourceNetId(AttackData? atk)
        {
            var attacker = atk?.source as Mob ?? atk?.carrier as Mob;
            if (attacker != null
                && session.ClientWorld?.TryGetMobNetId(
                    attacker, out uint netId) == true)
            {
                return netId;
            }

            return 0;
        }

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
            long? actionId = null,
            ShieldActionKind shieldKind = ShieldActionKind.HoldStart,
            bool fullParry = false,
            uint sourceNetId = 0,
            long sourceActionId = 0)
        {
            var world = session.ClientWorld;
            if (world == null) return;

            int currentEpoch = epoch ?? world.Epoch;
            if (currentEpoch < 0) return;

            var request = new HeroAction
            {
                Epoch = currentEpoch,
                ActionId = actionId ?? dedupe.NextActionId(),
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
                ShieldKind = shieldKind,
                FullParry = fullParry,
                SourceNetId = sourceNetId,
                SourceActionId = sourceActionId,
            };

            sender.Send(request);
        }
    }
}
