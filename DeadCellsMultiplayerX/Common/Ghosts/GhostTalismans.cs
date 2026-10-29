using dc;
using dc.tool;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Serializers;
using ModCore.Utilities;
using Serilog;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// GhostHero 的项链
    /// 使用 <see cref="Inventory.add"/> 装备
    /// </summary>
    internal sealed class GhostTalismans
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostTalismans");

        private readonly GhostActionContext context;

        /// <summary>
        /// 当前项链的物品签名
        /// </summary>
        private string? signature;

        public GhostTalismans(GhostActionContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// 应用服务端/客户端下发的项链
        /// null 或空 ItemId 表示清空当前项链
        /// </summary>
        public bool Apply(ItemRef? snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.ItemId))
            {
                Clear();
                return true;
            }

            string nextSignature = GhostLoadout.Signature(snapshot);
            var existing = Get();

            if (existing != null && signature == nextSignature)
                return true;

            // 项链必须存在于对应 cdb 中
            if (dc.Data.Class.item.byId.get(snapshot.ItemId.AsHaxeString()) == null)
            {
                Logger.Warning(
                    "GhostTalismans {Owner}: unknown talisman {ItemId}",
                    context.OwnerGuid, snapshot.ItemId);
                return false;
            }

            // Inventory.add 在已有项链时会抛 Already have a talisman
            if (existing != null)
                context.Hero.inventory.remove(existing);

            var item = ItemCodec.Create(
                snapshot, InventItemKind.Indexes.Talisman);

            context.Hero.inventory.add(item);
            signature = nextSignature;

            Logger.Debug("GhostTalismans {Owner}: equipped {ItemId} lv{Level}",
                context.OwnerGuid, snapshot.ItemId, snapshot.ItemLevel);
            return true;
        }

        /// <summary>
        /// 获取当前项链
        /// </summary>
        public InventItem? Get()
        {
            return context.Hero.inventory.hasTalisman()
                ? context.Hero.inventory.getTalisman()
                : null;
        }

        /// <summary>
        /// 卸下当前项链
        /// </summary>
        public void Clear()
        {
            var existing = Get();
            if (existing != null)
                context.Hero.inventory.remove(existing);

            signature = null;
        }

        /// <summary>
        /// 预留给后续项链状态 tick
        /// </summary>
        public void Tick(double dt)
        {
        }

        /// <summary>
        /// GhostHero 销毁时清空签名
        /// </summary>
        public void Dispose()
        {
            signature = null;
        }
    }
}
