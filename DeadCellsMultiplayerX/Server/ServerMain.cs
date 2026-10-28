using dc;
using dc.en;
using dc.en.hero;
using dc.en.inter;
using dc.en.mob;
using dc.h3d.mat;
using dc.haxe.io;
using dc.hxbit;
using dc.hxd;
using dc.hxd.res;
using dc.hxsl;
using dc.libs.heaps.slib;
using dc.pr;
using dc.tool;
using dc.tool.atk;
using DeadCellsMultiplayerX.Server.Events;
using Serilog;
using Hashlink;
using Hashlink.Marshaling;
using Hashlink.Proxy;
using Hashlink.Proxy.Clousre;
using Hashlink.Proxy.Objects;
using Hashlink.Reflection;
using Hashlink.Reflection.Types;
using HaxeProxy.Runtime;
using ModCore;
using ModCore.Events;
using ModCore.Events.Interfaces.Game;
using ModCore.Modules;
using ModCore.Utilities;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeadCellsMultiplayerX.Server
{
    internal class ServerMain : Module<ServerMain>,
        IOnBeforeGameInit
    {
        /// <summary>
        /// 服务器入口点
        /// 
        /// </summary>
        public static void Entrypoint()
        {
            _ = new ServerMain();

            // 启动游戏
            System.Type.GetType("ModCore.Startup, ModCore", true)!
                .GetMethod("StartGame")!
                .CreateDelegate<Func<int>>()
                ();
        }

        void IOnBeforeGameInit.OnBeforeGameInit()
        {
            Init();
        }

        public Thread? MainThread { get; private set; }

        public readonly string savePath = System.IO.Path.GetTempFileName();
        public readonly Dictionary<object, string> spriteLib2altas = [];

        private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "ServerMain");

        private ServerSession? session;

        internal ServerSession? Session => session;

        /// <summary>
        /// 初始化服务器
        /// </summary>
        internal void Init()
        {
            MainThread = Thread.CurrentThread;

            //隐藏服务器的窗口
            //Hook__Window.__constructor__ += Hook__Window___constructor__;
            Hook_Window.setFullScreen += Hook_Window_setFullScreen;

            //减少不必要的资源加载
            Hook_Loader.loadCache += Hook_Loader_loadCache;
            Hook_Texture.dispose += Hook_Texture_dispose;
            //Hook_Boot.render += Hook_Boot_render;
            //Hook_SpriteLib.getNormalMapFromGroup += Hook_SpriteLib_getNormalMapFromGroup;
            //Hook_SpriteLib.getNormalMapFromSprite += Hook_SpriteLib_getNormalMapFromSprite;

            dc.libs.heaps.slib.assets.Hook__Atlas.load += Hook__Atlas_load;
            Hook_Entity.setColorMap += Hook_Entity_setColorMap;
            Hook_Hero.initColorMap += Hook_Hero_initColorMap;

            //避免影响本地存档
            Hook__Save.fileName += Hook__Save_fileName;

            //避免影响本地配置
            var options = Options.Class.load();
            dc.tool.File.Class.PATH = System.IO.Path.GetDirectoryName(savePath)!.AsHaxeString();
            dc.Options.Class.FILE = System.IO.Path.GetFileName(System.IO.Path.GetTempFileName()).AsHaxeString();

            options.skipCinematics = true; //跳过动画
            options.disableLoreRooms = true; //关闭剧情房

            options.save();

            Hook_TitleScreen.initTitleScreen += Hook_TitleScreen_initTitleScreen;

            if (GameInfo.Platform == GameInfo.PlatformKind.Steam)
            {
                HashlinkHooks.Instance.CreateHook("tool.$File", "getSteamCloudStatus", Hook__File_getSteamCloudStatus);
                HashlinkHooks.Instance.CreateHook("tool.$File", "saveSteamCloudStatus", Hook__File_saveSteamCloudStatus);
            }

            Hook_Entity._isOnScreen += Hook_Entity__isOnScreen;
            Hook_Entity.canBeDetected += Hook_Entity_canBeDetected;
            // Hook_Entity.setGlowColor += Hook_Entity_setGlowColor;
            // Hook_Entity.setGlowData += Hook_Entity_setGlowData;
            Hook_Entity.dispose += Hook_Entity_dispose;

            //Hook_Game.init += Hook_Game_init;

            Hook_Level.attachMob += Hook_Level_attachMob;
            Hook__Mob.create += Hook__Mob_create;
            Hook_Mob.onDie += Hook_Mob_OnDie;
            Hook_Mob.preUpdate += Hook_Mob_preUpdate;
            Hook_Mob.fixedUpdate += Hook_Mob_fixedUpdate;
            Hook_Mob.postUpdate += Hook_Mob_postUpdate;
            Hook_Mob.onDamage += Hook_Mob_onDamage;
            Hook_Mob.onDirectHitFromHero += Hook_Mob_onDirectHitFromHero;
            Hook_Mob.updateThreat += Hook_Mob_updateThreat;
            Hook_Mob.setNemesisTarget += Hook_Mob_setNemesisTarget;
            Hook_Hero.fixedUpdate += Hook_Hero_fixedUpdateAsSelf;
            Hook_Hero.postUpdate += Hook_Hero_postUpdateAsSelf;
            Hook_Hero.onDamage += Hook_Hero_onDamageAsSelf;
            Hook_Hero.canBeHitBy += Hook_Hero_canBeHitBy;
            Hook_Power.update += Hook_Power_update;
            Hook__AttackUtils.applyHitResult += Hook_AttackUtils_applyHitResult;
            Hook_LevelAreaAffect.init += Hook_LevelAreaAffect_init;
        }


        private void Hook_Hero_fixedUpdateAsSelf(
            Hook_Hero.orig_fixedUpdate orig,
            Hero self)
        {
            if (session?.Ghosts.TryGetCombatant(self, out _) == true)
                session.Ghosts.Aggro.RunAsHero(self, () => orig(self));
            else
                orig(self);
        }

        private void Hook_Hero_postUpdateAsSelf(
            Hook_Hero.orig_postUpdate orig,
            Hero self)
        {
            if (session?.Ghosts.TryGetCombatant(self, out _) == true)
                session.Ghosts.Aggro.RunAsHero(self, () => orig(self));
            else
                orig(self);
        }

        private void Hook_Hero_onDamageAsSelf(
            Hook_Hero.orig_onDamage orig,
            Hero self,
            AttackData atk)
        {
            if (session?.Ghosts.TryGetCombatant(self, out _) == true)
                session.Ghosts.Aggro.RunAsHero(self, () => orig(self, atk));
            else
                orig(self, atk);
        }

        /// <summary>
        /// 服务端 Ghost 不参与暂停/电影状态判断，否则原版 updateThreat 会反复清目标
        /// </summary>
        private bool Hook_Hero_canBeHitBy(
            Hook_Hero.orig_canBeHitBy orig,
            Hero self,
            Entity by)
        {
            if (session?.Ghosts.TryGetCombatant(self, out _) == true)
                return !self.destroyed && self.life > 0;

            return orig(self, by);
        }

        /// <summary>
        /// 原版 updateThreat 之后只做联机目标修复
        /// </summary>
        private void Hook_Mob_updateThreat(
            Hook_Mob.orig_updateThreat orig,
            Mob self)
        {
            orig(self);
            session?.Ghosts.Aggro.OnThreatUpdated(self);
        }

        /// <summary>
        /// 阻止 mob 硬锁定服务端假人，改选最近的 Ghost
        /// </summary>
        private void Hook_Mob_setNemesisTarget(
            Hook_Mob.orig_setNemesisTarget orig,
            Mob self,
            Entity target)
        {
            if (session?.Ghosts.Aggro.TryRedirectNemesis(
                    self, target, out var redirected) == true)
            {
                if (redirected == null)
                {
                    self.clearNemesisTarget();
                    return;
                }

                target = redirected;
            }

            orig(self, target);
        }

        //打印命中数据
        private void Hook_AttackUtils_applyHitResult(
            Hook__AttackUtils.orig_applyHitResult orig,
            AttackData atk,
            Entity target)
        {
            // mob 命中 Ghost：服务端只判定命中，生命由 owner 客户端结算
            if (target is Hero targetHero
                && session?.Ghosts.TryGetCombatant(
                    targetHero, out var ghost) == true)
            {
                var hit = session.Ghosts.RecordIncomingHit(ghost!, atk);
                if (hit != null)
                    session.SendGhostHit(hit);

                return;
            }

#if DEBUG
            var gameHero = dc.pr.Game.Class.ME?.hero;
            if (atk?.source is Hero hero
                && !ReferenceEquals(hero, gameHero)
                && target is Mob mob)
            {
                Log.Information(
                    "Ghost attack hit: source={Source} target={Target} " +
                    "finalDmg={FinalDmg} hit={HitResult} targetLife={Life}",
                    hero.type, mob.GetType().Name,
                    atk.finalDmg, atk.hitResult, mob.life);
            }
#endif
            orig(atk, target);
        }

        /// <summary>
        /// 登记服务端运行时创建的动态关卡区域
        /// </summary>
        private void Hook_LevelAreaAffect_init(
            Hook_LevelAreaAffect.orig_init orig,
            LevelAreaAffect self,
            int x,
            int y,
            double durationS,
            int a,
            double aDurationS,
            double aValue,
            dc.hl.types.ArrayObj affixes)
        {
            orig(self, x, y, durationS, a, aDurationS, aValue, affixes);
            session?.AreaAffects.Register(self);
        }

        /// <summary>
        /// Ghost 释放的 Power 在 owner 被销毁后仍然会 update
        /// </summary>
        private void Hook_Power_update(Hook_Power.orig_update orig, Power self)
        {
            if (self == null) return;

            var owner = self.owner;
            if (owner == null || owner._level == null)
            {
                self.destroy();
                return;
            }

            orig(self);
        }


        private void Hook_Mob_OnDie(Hook_Mob.orig_onDie orig, Mob self)
        {
            if (session != null)
                session.Ghosts.Aggro.RunAsMobHero(self, () => orig(self));
            else
                orig(self);

            session?.MobRegistry.MarkDead(self);
            EventSystem.BroadcastEvent<IOnMobOnDie, dc.en.Mob>(self);
        }

        private void Hook_Mob_preUpdate(Hook_Mob.orig_preUpdate orig, Mob self)
        {
            if (session != null)
                session.Ghosts.Aggro.RunAsMobHero(self, () => orig(self));
            else
                orig(self);
        }

        private void Hook_Mob_fixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self)
        {
            if (session != null)
                session.Ghosts.Aggro.RunAsMobHero(self, () => orig(self));
            else
                orig(self);
        }

        private void Hook_Mob_postUpdate(Hook_Mob.orig_postUpdate orig, Mob self)
        {
            if (session != null)
                session.Ghosts.Aggro.RunAsMobHero(self, () => orig(self));
            else
                orig(self);
        }

        private void Hook_Mob_onDamage(
            Hook_Mob.orig_onDamage orig,
            Mob self,
            AttackData atk)
        {
            if (session != null)
            {
                var hero = session.Ghosts.Aggro.ResolveAttackerHero(atk)
                    ?? session.Ghosts.Aggro.ResolveMobHero(self);
                session.Ghosts.Aggro.RunAsHero(
                    hero, () => orig(self, atk));
            }
            else
            {
                orig(self, atk);
            }
        }

        private void Hook_Mob_onDirectHitFromHero(
            Hook_Mob.orig_onDirectHitFromHero orig,
            Mob self,
            AttackData atk)
        {
            if (session != null)
            {
                var hero = session.Ghosts.Aggro.ResolveAttackerHero(atk)
                    ?? session.Ghosts.Aggro.ResolveMobHero(self);
                session.Ghosts.Aggro.RunAsHero(
                    hero, () => orig(self, atk));
            }
            else
            {
                orig(self, atk);
            }
        }

        private Mob Hook__Mob_create(Hook__Mob.orig_create orig, dc.String k, Level level, int cx, int cy, int dmgTier, Ref<int> lifeTier)
        {
            var mob = orig(k, level, cx, cy, dmgTier, lifeTier);

            session?.MobRegistry.Register(mob, null, level);
            int? safelifeTier = lifeTier.IsNull ? 0 : lifeTier.value;
            EventSystem.BroadcastEvent<IOnCreateMob, IOnCreateMob.Data>(new(mob, k, cx, cy, dmgTier, safelifeTier));
            return mob;
        }

        private dc.en.Mob Hook_Level_attachMob(Hook_Level.orig_attachMob orig, Level self, dc.level.Mob m)
        {
            var mob = orig(self, m);

            session?.MobRegistry.Register(mob, m, self);
            EventSystem.BroadcastEvent<IOnAttachMob, IOnAttachMob.Data>(new(m, mob));

            return mob;
        }

        private void Hook_Game_init(Hook_Game.orig_init orig, dc.pr.Game self)
        {
            orig(self);
            self.gameSignals.heroInitDone.add(new HlAction<Hero>((hero) => EventSystem.BroadcastEvent<IOnHeroInitDone, Hero>(hero)),
            null, null, null);
        }

        private void Hook_Entity_dispose(Hook_Entity.orig_dispose orig, Entity self)
        {
            if (self is Mob mob)
                session?.MobRegistry.Unregister(mob);

            orig(self);

            EventSystem.BroadcastEvent<IOnEntityDisposed, Entity>(self);
        }

        private void Hook__File_saveSteamCloudStatus(HashlinkClosure orig)
        {

        }

        private bool? Hook__File_getSteamCloudStatus(HashlinkClosure orig)
        {
            return null;
        }

        private void Hook_Entity_setGlowData(Hook_Entity.orig_setGlowData orig, Entity self, int index,
            Hashlink.Virtuals.virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_ glowData,
            HSprite sprite)
        {
            EventSystem.BroadcastEvent<IOnEntitySetGlowData, IOnEntitySetGlowData.Data>(new(self, index, glowData));

            orig(self, index, glowData, sprite);
        }

        private void Hook_Entity_setGlowColor(Hook_Entity.orig_setGlowColor orig, Entity self, int inner, int? outer, double? power, HSprite sprite)
        {
            self.setGlowData(0, new()
            {
                inner = inner,
                outer = outer,
                power = power
            }, sprite);
        }

        private bool Hook_Entity__isOnScreen(Hook_Entity.orig__isOnScreen orig, Entity self)
        {
            return true;
        }

        /// <summary>
        /// 不能被隐身或 ignoreDetection 清掉目标
        /// </summary>
        private bool Hook_Entity_canBeDetected(
            Hook_Entity.orig_canBeDetected orig,
            Entity self)
        {
            if (session?.Ghosts.TryGetCombatant(self, out _) == true)
                return !self.destroyed;

            return orig(self);
        }

        private void Hook_Hero_initColorMap(Hook_Hero.orig_initColorMap orig, Hero self)
        {
            orig(self);

            var inf = self.getSkinInfo();

            EventSystem.BroadcastEvent<IOnEntitySetColorMap, IOnEntitySetColorMap.Data>(new(self, inf.model.ToString(), inf.colorMap.ToString()));
        }

        private void Hook_Entity_setColorMap(Hook_Entity.orig_setColorMap orig, Entity self, dc.String model, dc.String skin, HSprite sspr)
        {
            orig(self, model, skin, sspr);

            EventSystem.BroadcastEvent<IOnEntitySetColorMap, IOnEntitySetColorMap.Data>(new(self, model.ToString(), skin.ToString()));
        }

        private SpriteLib Hook__Atlas_load(dc.libs.heaps.slib.assets.Hook__Atlas.orig_load orig, dc.String atlasPath,
            HaxeProxy.Runtime.HlAction onReload, dc.hl.types.ArrayObj notZeroBaseds, dc.hl.types.ArrayObj properties)
        {
            var lib = orig(atlasPath, onReload, notZeroBaseds, properties);
            spriteLib2altas[lib] = atlasPath.ToString();
            return lib;
        }

        private void Hook_TitleScreen_initTitleScreen(Hook_TitleScreen.orig_initTitleScreen orig, TitleScreen self,
            SpriteLib titleLib, HaxeProxy.Runtime.Ref<int> bgType)
        {
            orig(self, titleLib, bgType);

            if (session != null)
            {
                return;
            }

            session = new();
            _ = session.Init();
        }

        private void Hook_Boot_render(Hook_Boot.orig_render orig, Boot self, dc.h3d.Engine e)
        {

        }


        private dc.String Hook__Save_fileName(Hook__Save.orig_fileName orig, int? slot)
        {
            return System.IO.Path.GetFileName(savePath).AsHaxeString();
        }

        private void Hook_Window_setFullScreen(Hook_Window.orig_setFullScreen orig, Window self, bool v)
        {

        }

        private void Hook__Window___constructor__(Hook__Window.orig___constructor__ orig, Window arg1, dc.String title, int width, int height)
        {
            orig(arg1, title, 1, 1);

            SDL2.SDL.SDL_HideWindow(arg1.window.win);
        }

        private Texture Hook_SpriteLib_getNormalMapFromSprite(Hook_SpriteLib.orig_getNormalMapFromSprite orig, SpriteLib self, HSprite sprite)
        {
            return null!;
        }

        private Texture Hook_SpriteLib_getNormalMapFromGroup(Hook_SpriteLib.orig_getNormalMapFromGroup orig, SpriteLib self, dc.String groupName)
        {
            return null!;
        }

        private void Hook_Texture_dispose(Hook_Texture.orig_dispose orig, Texture self)
        {

        }

        private Image? img4096x4096;
        private Sound? soundEmpty;

        private Resource Hook_Loader_loadCache(Hook_Loader.orig_loadCache orig, Loader self, dc.String path, dc.hl.Class c)
        {
            var p = path.ToString();

            if (c == Image.Class)
            {
                //return img4096x4096 ??= new Image(self.load("atlas/beheaded0.png".AsHaxeString()).entry);
            }
            else if (c == Sound.Class)
            {
                return soundEmpty ??= new Sound(self.load("sfx/noSound_Error.wav".AsHaxeString()).entry);
            }

            return orig(self, path, c);

        }

    }
}
