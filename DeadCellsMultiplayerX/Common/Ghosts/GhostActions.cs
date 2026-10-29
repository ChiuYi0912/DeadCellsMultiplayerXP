using System.Diagnostics;
using dc.en;
using dc.tool;
using dc.tool.hero;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// GhostHero用于控制客户端或者服务端的hero
    /// </summary>
    internal sealed class GhostActions : IDisposable
    {
        /// <summary>
        /// 技能释放系统
        /// </summary>
        private readonly GhostSkills skills;

        /// <summary>
        /// 武器释放系统
        /// </summary>
        private readonly GhostWeapons weapons;

        /// <summary>
        /// 项链装备系统
        /// </summary>
        private readonly GhostTalismans talismans;

        /// <summary>
        /// 为了安全管理关卡切换时Null引用
        /// </summary>
        private bool disposed;

        public GhostActions(Hero hero, string ownerGuid)
        {
            ArgumentNullException.ThrowIfNull(hero);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerGuid);

            var context = new GhostActionContext(hero, ownerGuid);
            skills = new GhostSkills(context);
            weapons = new GhostWeapons(context);
            talismans = new GhostTalismans(context);
        }

        #region Tool
        /// <summary>
        /// 从当前hero的装备栏获取对应武器
        /// </summary>
        /// <param name="slot">1或0 对应武器栏</param>
        /// <returns></returns>
        public Weapon? GetWeapon(int slot) => weapons.Get(slot);

        /// <summary>
        /// 从当前hero的装备栏获取对应技能
        /// </summary>
        /// <param name="slot">1或0 对应技能栏<</param>
        /// <returns></returns>
        public HeroActiveSkill? GetActiveSkill(int slot) => skills.GetActive(slot);

        /// <summary>
        /// 获取当前项链
        /// </summary>
        public InventItem? GetTalisman() => talismans.Get();

        #endregion

        #region Skills

        /// <summary>
        /// 将对应<see cref="ItemRef"/>装备至 slot
        /// </summary>
        /// <param name="slot">1或0 对应Hero技能栏</param>
        /// <param name="snapshot">客户端或服务端发送的物品快照</param>
        /// <returns>bool 是否释放成功</returns>
        public bool PrepareSkill(int slot, ItemRef? snapshot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return skills.Prepare(slot, snapshot);
        }

        /// <summary>
        /// 判断技能冷却是否结束
        /// </summary>
        /// <param name="slot">1或0 对应Hero技能栏</param>
        /// <returns>bool 是否准备好</returns>
        public bool IsSkillReady(int slot) => skills.IsReady(slot);

        /// <summary>
        /// 服务端释放技能
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="ratio"></param>
        /// <returns>是否成功释放</returns>
        public bool CastSkill(int slot, double ratio)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return skills.Cast(slot, ratio);
        }

        /// <summary>
        /// 客户端释放技能
        /// </summary>
        /// <param name="slot"></param>
        /// <returns>bool 是否准备好</returns>
        public bool ReplaySkill(int slot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return skills.Replay(slot);
        }

        #endregion

        #region Weapons

        /// <summary>
        /// 装备对应武器
        /// </summary>
        /// <param name="slot">1或0 对应武器栏</param>
        /// <param name="snapshot">快照</param>
        /// <returns>bool 是否装备成功</returns>
        public bool PrepareWeapon(int slot, ItemRef? snapshot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return weapons.Prepare(slot, snapshot);
        }

        /// <summary>
        /// 使用武器
        /// </summary>
        /// <param name="slot">1或0 对应武器栏</param>
        /// <param name="cycle">武器的连段</param>
        /// <returns>bool 是否使用成功</returns>
        public bool UseWeapon(int slot, int cycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return weapons.Use(slot, cycle);
        }

        #endregion

        #region Talismans

        /// <summary>
        /// 装备或清除项链
        /// </summary>
        /// <param name="snapshot">项链快照；null 表示清除</param>
        /// <returns>bool 是否应用成功</returns>
        public bool ApplyTalisman(ItemRef? snapshot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return talismans.Apply(snapshot);
        }

        #endregion

        #region Lifecycle

        public void Tick(double dt)
        {
            if (disposed) return;

            skills.Tick(dt);
            weapons.Tick(dt);
            talismans.Tick(dt);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                skills.Dispose();
            }
            finally
            {
                try
                {
                    weapons.Dispose();
                }
                finally
                {
                    talismans.Dispose();
                }
            }
        }

        #endregion

        [Conditional("DEBUG")]
        public void DiagnoseWeapon(int slot)
            => weapons.Diagnose(slot);
    }
}
