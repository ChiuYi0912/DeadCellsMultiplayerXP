using System;
using System.Collections.Generic;
using System.Linq;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Ghosts;
using Serilog;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Ghosts.Execution
{
    /// <summary>
    /// 按类型执行GhostHero行为
    /// </summary>
    internal interface IActionHandler
    {
        HeroActionKind Kind { get; }

        bool TryExecute(
            GhostHero ghost,
            HeroAction request,
            double ratio);
    }

    /// <summary>
    /// 执行对应action
    /// </summary>
    internal sealed class ActionExecutor
    {
        private readonly Dictionary<HeroActionKind, IActionHandler> handlers;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ActionExecutor");

        private ActionExecutor(IEnumerable<IActionHandler> handlers)
        {
            this.handlers = handlers.ToDictionary(handler => handler.Kind);
        }

        /// <summary>
        /// 根据<see cref="HeroActionKind"/>执行武器释放,技能释放,佩戴项链
        /// </summary>
        public static ActionExecutor CreateDefault()
        {
            IActionHandler[] handlers =
            [
                new SkillActionHandler(),
                new WeaponActionHandler(),
                new TalismanActionHandler(),
            ];

            return new ActionExecutor(handlers);
        }

        /// <summary>
        /// 执行一次已通过准入的动作
        /// </summary>
        public bool TryExecute(
            GhostHero ghost,
            HeroAction request,
            double ratio)
        {
            ghost.SetTransform(
                request.CX, request.CY, request.XR, request.YR,
                request.Dir, request.DX, request.DY);

            int brutality = Math.Clamp(
                request.BrutalityTier, 0, ActionPolicy.MaxTier);
            int survival = Math.Clamp(
                request.SurvivalTier, 0, ActionPolicy.MaxTier);
            int tactic = Math.Clamp(
                request.TacticTier, 0, ActionPolicy.MaxTier);

            if (ghost.Hero.brutalityTier != brutality
                || ghost.Hero.survivalTier != survival
                || ghost.Hero.tacticTier != tactic)
            {
                ghost.SetTiers(brutality, survival, tactic);
            }

            return handlers.TryGetValue(request.Kind, out var handler)
                && handler.TryExecute(ghost, request, ratio);
        }
    }
}
