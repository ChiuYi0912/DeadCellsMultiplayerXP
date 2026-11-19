namespace DeadCellsMultiplayerX.Engine.Adapters.Levels;

using dc;
using dc.achievements;
using dc.cine;
using dc.hl.types;
using dc.level;
using dc.pr;
using HashlinkNET.Native.Impl;
using HaxeProxy.Runtime;

/// <summary>Native loadNewLevel behavior that preserves onEnteredLevel subscribers.</summary>
internal static class NativeLevelTransition
{
    public static void Load(LevelTransition self)
    {
        if (self.heroPosAfterBossRuneReload != null)
        {
            var seed = Game.Class.ME.curLevel.map?.seed;
            Game.Class.ME.loadMainLevel(self, self.mainId, Ref<bool>.In(true), seed);
        }
        else if (self.mainId != null)
        {
            Game.Class.ME.loadMainLevel(self, self.mainId, default, null);
        }
        else
        {
            Game.Class.ME.activateSubLevel(self.map, self.linkId,
                Ref<bool>.In(true), Ref<bool>.In(self.playAfterZDoorCine));
        }

        Lib_std.gc_major.Invoke();

        if (self.get_isADlcPLevel())
        {
            self.onEnteredLevel ??= new HlAction(() => { });
#pragma warning disable CA1806 // 不要忽略方法结果
            self.onEnteredLevel += () => new PurpleLevelIntro(self.mainId, Game.Class.ME.hero);
#pragma warning restore CA1806 // 不要忽略方法结果
        }

        Main.Class.ME.fadeOut(Ref<double>.In(self.multFade));
        self.afterTransitionCine();
        if (self.walk == null && self.jump == null && self.climb == null && self.onEnteredLevel != null)
        {
            self.onEnteredLevel.Invoke();
        }

        if (self.heroPosAfterBossRuneReload != null)
        {
            var flaskRoom = FindFlaskRoom(Game.Class.ME.curLevel.map?.rooms!);
            if (flaskRoom != null)
            {
                Game.Class.ME.hero.setPosCase(
                    flaskRoom.x + self.heroPosAfterBossRuneReload.cx,
                    flaskRoom.y + self.heroPosAfterBossRuneReload.cy, null!, null!);
                Game.Class.ME.curLevel.afterBossRuneReload();
            }
        }

        if (self.giveGentlemanAchievement)
        {
            Achievements.Class.setAchievement(new EAchievement.FEAT_PRISON_NOBREAK_DOOR(), default);
        }

        self.onLoad?.Invoke();
    }

    private static Room FindFlaskRoom(ArrayObj rooms)
    {
        if (rooms == null)
        {
            return null!;
        }

        for (var i = 0; i < rooms.length; i++)
        {
            Room room = rooms.getDyn(i);
            if (room != null && room.rType.ToString() == "FlaskRoom")
            {
                return room;
            }
        }
        return null!;
    }
}
