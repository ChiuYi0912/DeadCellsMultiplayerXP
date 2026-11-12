using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Loadout;
namespace DeadCellsMultiplayerX.Simulation.Loadout
{
    public static class LoadoutCodec
    {
        public static ItemDef? ToDef(ItemRef? item)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemId))
                return null;

            return new ItemDef
            {
                Kind = item.Kind,
                ItemId = item.ItemId,
                Level = item.ItemLevel,
                PermanentId = item.PermanentId,
                Affixes = new List<string>(item.Affixes ?? []),
                InactiveAffixes = new List<string>(item.InactiveAffixes ?? []),
            };
        }

        public static ItemRef? ToItemRef(ItemDef? def, ItemRuntime? runtime, int posId)
        {
            if (def == null || string.IsNullOrEmpty(def.ItemId))
                return null;

            return new ItemRef
            {
                Kind = def.Kind,
                ItemId = def.ItemId,
                ItemLevel = def.Level,
                PosID = posId,
                PermanentId = def.PermanentId,
                Ammo = runtime?.Ammo ?? 0,
                Affixes = new List<string>(def.Affixes ?? []),
                InactiveAffixes = new List<string>(def.InactiveAffixes ?? []),
            };
        }

        public static EquipmentSlot CreateSlot(ItemRef? item)
        {
            return new EquipmentSlot
            {
                Def = ToDef(item),
                Runtime = new ItemRuntime
                {
                    Ammo = item?.Ammo ?? 0,
                },
            };
        }

        public static ItemRef? ToItemRef(EquipmentSlot? slot, int posId)
            => slot == null
                ? null
                : ToItemRef(slot.Def, slot.Runtime, posId);

        /// <summary>
        /// 计算稳定的装备定义版本。
        /// 只包含定义与天赋，不包含弹药/冷却等运行时状态。
        /// </summary>
        public static uint ComputeVersion(PlayerLoadout loadout)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash, loadout.BrutalityTier);
                hash = Mix(hash, loadout.SurvivalTier);
                hash = Mix(hash, loadout.TacticTier);

                foreach (var slot in loadout.Weapons)
                    hash = MixSlot(hash, slot);

                foreach (var slot in loadout.Actives)
                    hash = MixSlot(hash, slot);

                return MixSlot(hash, loadout.Talisman);
            }
        }

        private static uint MixSlot(uint hash, EquipmentSlot? slot)
        {
            var def = slot?.Def;
            if (def == null)
                return Mix(hash, -1);

            hash = Mix(hash, def.Kind);
            hash = Mix(hash, def.ItemId);
            hash = Mix(hash, def.Level);
            hash = Mix(hash, def.PermanentId);

            foreach (var affix in def.Affixes ?? [])
                hash = Mix(hash, affix);

            foreach (var affix in def.InactiveAffixes ?? [])
                hash = Mix(hash, affix);

            return hash;
        }

        private static uint Mix(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                return hash * 16777619u;
            }
        }

        private static uint Mix(uint hash, string? value)
        {
            unchecked
            {
                foreach (char c in value ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }

                hash ^= 0xFFu;
                return hash * 16777619u;
            }
        }
    }
}
