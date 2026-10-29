using System.Diagnostics;
using dc.en;
using Serilog;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// 技能/武器动作模块共享的只读上下文
    /// 只提供 Hero、owner 和诊断辅助
    /// </summary>
    internal sealed class GhostActionContext
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostActions");

        public GhostActionContext(Hero hero, string ownerGuid)
        {
            Hero = hero;
            OwnerGuid = ownerGuid;
        }

        /// <summary>
        /// 客户端在服务端的隐形 Hero
        /// </summary>
        public Hero Hero { get; }

        /// <summary>
        /// 客户端id
        /// </summary>
        public string OwnerGuid { get; }

        /// <summary>
        /// 当前 Level 的实体与 Power 数量
        /// </summary>
        public (int Entities, int Powers) Count()
        {
            var level = Hero._level;
            return (level?.entities?.length ?? 0, level?.powers?.length ?? 0);
        }

        /// <summary>
        /// DEBUG 下记录一次动作造成的实体/Power 增量
        /// </summary>
        [Conditional("DEBUG")]
        public void LogActionDelta(
            string action,
            int slot,
            (int Entities, int Powers) before)
        {
            var level = Hero._level;
            int entities = level?.entities?.length ?? 0;
            int powers = level?.powers?.length ?? 0;
            string levelId = level?.map?.id?.ToString() ?? string.Empty;

            Logger.Verbose(
                "GhostHero {Action}: owner={Owner} slot={Slot} level={Level} " +
                "entities {OldEntities}->{NewEntities} powers {OldPowers}->{NewPowers}",
                action, OwnerGuid, slot, levelId,
                before.Entities, entities, before.Powers, powers);
        }
    }
}
