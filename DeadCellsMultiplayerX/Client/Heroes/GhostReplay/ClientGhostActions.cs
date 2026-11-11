using System;
using dc.en;
using dc.tool;
using dc.tool.atk;
using DeadCellsMultiplayerX.Common.Contracts.Loadout;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Ghosts;

namespace DeadCellsMultiplayerX.Client.Heroes.GhostReplay
{
    internal sealed class ClientGhostActions : IDisposable
    {
        private readonly GhostSkills skills;
        private readonly GhostWeapons weapons;
        private readonly GhostTalismans talismans;
        private readonly GhostShield shield;

        private bool disposed;

        public ClientGhostActions(Hero hero, string ownerGuid)
        {
            ArgumentNullException.ThrowIfNull(hero);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerGuid);

            var context = new GhostActionContext(hero, ownerGuid);
            skills = new GhostSkills(context);
            weapons = new GhostWeapons(context);
            talismans = new GhostTalismans(context);
            shield = new GhostShield(weapons);
        }

        public bool ApplyLoadout(PlayerLoadout? loadout)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (loadout == null) return false;

            bool ok = true;

            for (int i = 0; i < loadout.Weapons.Count && i < 2; i++)
            {
                var item = LoadoutCodec.ToItemRef(loadout.Weapons[i], i);
                if (item == null) continue;
                ok &= weapons.Prepare(i, item);
            }

            for (int i = 0; i < loadout.Actives.Count && i < 2; i++)
            {
                var item = LoadoutCodec.ToItemRef(loadout.Actives[i], i);
                if (item == null) continue;
                ok &= skills.Prepare(i, item);
            }

            // null 表示清空项链
            ok &= talismans.Apply(
                LoadoutCodec.ToItemRef(loadout.Talisman, 0));

            return ok;
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

        public bool PrepareShield(int slot, ItemRef? snapshot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return shield.Prepare(slot, snapshot);
        }

        public bool UseShield(
            ShieldActionKind kind,
            int slot,
            double ratio,
            bool fullParry,
            AttackData? sourceAtk,
            double elapsedMs)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return shield.Apply(kind, slot, ratio, fullParry, sourceAtk, elapsedMs);
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
