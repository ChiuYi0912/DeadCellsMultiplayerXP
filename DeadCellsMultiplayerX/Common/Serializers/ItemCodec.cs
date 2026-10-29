using System;
using System.Collections.Generic;
using dc.hl.types;
using dc.tool;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Serializers
{
    /// <summary>
    /// InventItem 与可传输快照之间的转换
    /// </summary>
    public static class ItemCodec
    {
        public static ItemRef Collect(InventItem item)
        {
            var snapshot = new ItemRef
            {
                Kind = (int)item.kind.Index,
                ItemId = GetItemId(item),
                ItemLevel = item._itemLevel,
                PosID = item.posID,
                PermanentId = item.permanentId,
                Ammo = item.ammo,
            };

            CollectAffixes(item.affixes, snapshot.Affixes);
            CollectAffixes(item.inactiveAffixes, snapshot.InactiveAffixes);

            return snapshot;
        }

        public static InventItem Create(
            ItemRef snapshot,
            InventItemKind.Indexes? forcedKind = null)
        {
            var kind = CreateKind(snapshot, forcedKind);
            var item = new InventItem(kind)
            {
                posID = snapshot.PosID,
                _itemLevel = System.Math.Max(1, snapshot.ItemLevel),
                ammo = snapshot.Ammo,
            };

            if (snapshot.PermanentId != 0)
                item.permanentId = snapshot.PermanentId;

            if (item.affixes == null)
                item.affixes = (ArrayObj)ArrayUtils.CreateDyn().array;

            foreach (var affix in snapshot.Affixes)
            {
                if (!string.IsNullOrEmpty(affix))
                    item.affixes.push(affix.AsHaxeString());
            }

            if (item.inactiveAffixes == null)
                item.inactiveAffixes = (ArrayObj)ArrayUtils.CreateDyn().array;

            foreach (var affix in snapshot.InactiveAffixes)
            {
                if (!string.IsNullOrEmpty(affix))
                    item.inactiveAffixes.push(affix.AsHaxeString());
            }

            return item;
        }

        private static void CollectAffixes(ArrayObj? affixes, List<string> target)
        {
            if (affixes == null) return;

            for (int i = 0; i < affixes.length; i++)
            {
                string? affix = affixes.getDyn(i)?.ToString();
                if (!string.IsNullOrEmpty(affix))
                    target.Add(affix);
            }
        }

        public static string GetItemId(InventItem item)
        {
            return item.kind switch
            {
                InventItemKind.Weapon weapon => weapon.Param0?.ToString() ?? string.Empty,
                InventItemKind.Active active => active.Param0?.ToString() ?? string.Empty,
                InventItemKind.Talisman talisman => talisman.Param0?.ToString() ?? string.Empty,
                InventItemKind.BagItem bag => bag.Param0?.ToString() ?? string.Empty,
                InventItemKind.Consumable consumable => consumable.Param0?.ToString() ?? string.Empty,
                InventItemKind.PreciousLoot loot => loot.Param0?.ToString() ?? string.Empty,
                InventItemKind.Meta meta => meta.Param0?.ToString() ?? string.Empty,
                InventItemKind.Perk perk => perk.Param0?.ToString() ?? string.Empty,
                InventItemKind.Skin skin => skin.Param0?.ToString() ?? string.Empty,
                InventItemKind.Head head => head.Param0?.ToString() ?? string.Empty,
                InventItemKind.Aspect aspect => aspect.Param0?.ToString() ?? string.Empty,
                _ => string.Empty,
            };
        }

        private static InventItemKind CreateKind(
            ItemRef snapshot,
            InventItemKind.Indexes? forcedKind)
        {
            dc.String id = snapshot.ItemId.AsHaxeString();
            int kind = forcedKind.HasValue ? (int)forcedKind.Value : snapshot.Kind;

            return kind switch
            {
                (int)InventItemKind.Indexes.Weapon => new InventItemKind.Weapon(id),
                (int)InventItemKind.Indexes.Active => new InventItemKind.Active(id),
                (int)InventItemKind.Indexes.Talisman => new InventItemKind.Talisman(id),
                (int)InventItemKind.Indexes.BagItem => new InventItemKind.BagItem(id),
                (int)InventItemKind.Indexes.Consumable => new InventItemKind.Consumable(id),
                (int)InventItemKind.Indexes.PreciousLoot => new InventItemKind.PreciousLoot(id),
                (int)InventItemKind.Indexes.Meta => new InventItemKind.Meta(id),
                (int)InventItemKind.Indexes.Perk => new InventItemKind.Perk(id),
                (int)InventItemKind.Indexes.Skin => new InventItemKind.Skin(id),
                (int)InventItemKind.Indexes.Head => new InventItemKind.Head(id),
                (int)InventItemKind.Indexes.Aspect => new InventItemKind.Aspect(id),
                _ => new InventItemKind.Active(id),
            };
        }
    }
}
