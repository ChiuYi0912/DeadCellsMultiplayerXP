using System;
using dc.en;
using dc.tool;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Ghosts;

namespace DeadCellsMultiplayerX.Client.Heroes.GhostReplay
{
    /// <summary>
    /// 客户端 Ghost 的表现动作入口。
    /// 只做重放与视觉，不产生权威伤害。
    /// </summary>
    internal sealed class ClientGhostActions : IDisposable
    {
        private readonly GhostSkills skills;
        private readonly GhostWeapons weapons;
        private readonly GhostTalismans talismans;

        private bool disposed;

        public ClientGhostActions(Hero hero, string ownerGuid)
        {
            ArgumentNullException.ThrowIfNull(hero);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerGuid);

            var context = new GhostActionContext(hero, ownerGuid);
            skills = new GhostSkills(context);
            weapons = new GhostWeapons(context);
            talismans = new GhostTalismans(context);
        }

        public bool PrepareSkill(int slot, ItemRef? snapshot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return skills.Prepare(slot, snapshot);
        }

        public bool ReplaySkill(int slot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return skills.Replay(slot);
        }

        public bool PrepareWeapon(int slot, ItemRef? snapshot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return weapons.Prepare(slot, snapshot);
        }

        public bool UseWeapon(int slot, int cycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return weapons.Use(slot, cycle);
        }

        public bool ApplyTalisman(ItemRef? snapshot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return talismans.Apply(snapshot);
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
    }
}
