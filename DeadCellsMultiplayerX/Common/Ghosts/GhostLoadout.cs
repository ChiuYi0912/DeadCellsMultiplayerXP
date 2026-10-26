using System;
using dc;
using dc.tool;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Serializers;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// 按物品池管理 GhostHero 的 2 个槽位
    /// </summary>
    internal sealed class GhostLoadout
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostLoadout");

        private readonly GhostActionContext context;
        private readonly InventItemKind.Indexes kind;
        private readonly string?[] signatures = new string?[2];

        public GhostLoadout(GhostActionContext context, InventItemKind.Indexes kind)
        {
            this.context = context;
            this.kind = kind;
        }

        /// <summary>
        /// 准备对应槽位的物品
        /// 签名一致时复用，否则重建并写回 Hero 背包
        /// </summary>
        public bool TryPrepare(int slot, ItemRef? snapshot, out InventItem? item, out bool changed)
        {
            item = null;
            changed = false;

            if (snapshot == null || string.IsNullOrEmpty(snapshot.ItemId))
                return false;

            if (slot is < 0 or > 1)
                return false;

            string signature = Signature(snapshot);
            var existing = GetEquipped(slot);

            if (signatures[slot] == signature && existing != null)
            {
                // 签名一致不重建
                item = existing;
                return true;
            }

            // 必须在对应 cdb 中存在
            if (dc.Data.Class.item.byId.get(snapshot.ItemId.AsHaxeString()) == null)
            {
                Logger.Warning(
                    "GhostLoadout {Owner}: unknown {Kind} item {ItemId}",
                    context.OwnerGuid, kind, snapshot.ItemId);
                return false;
            }

            var newItem = ItemCodec.Create(snapshot, kind);
            newItem.posID = slot;

            if (existing != null)
                context.Hero.inventory.remove(existing);

            context.Hero.inventory.items.push(newItem);
            signatures[slot] = signature;

            item = newItem;
            changed = true;

            Logger.Debug("GhostLoadout {Owner}: slot {Slot} {Kind} item {ItemId} lv{Level}",
                context.OwnerGuid, slot, kind, snapshot.ItemId, snapshot.ItemLevel);
            return true;
        }

        /// <summary>
        /// 获取槽位当前物品
        /// </summary>
        public InventItem? GetEquipped(int slot)
            => kind == InventItemKind.Indexes.Active
                ? context.Hero.inventory.getActiveOn(slot)
                : context.Hero.inventory.getEquippedWeaponOn(slot);

        /// <summary>
        /// 清空签名记录，不删除 Hero 身上的物品
        /// </summary>
        public void Clear()
        {
            Array.Clear(signatures);
        }

        /// <summary>
        /// 生成物品签名
        /// </summary>
        internal static string Signature(ItemRef snapshot)
            => $"{snapshot.ItemId}|{snapshot.ItemLevel}|{snapshot.PermanentId}|" +
               $"{snapshot.Ammo}|{string.Join(',', snapshot.Affixes)}|" +
               string.Join(',', snapshot.InactiveAffixes);
    }
}
