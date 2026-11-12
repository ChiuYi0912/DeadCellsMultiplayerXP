using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using Serilog;
using DeadCellsMultiplayerX.Server.World.Actors;
namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution
{
    internal sealed class ActionExecutor
    {
        private readonly Dictionary<HeroActionKind, IServerActionModule> modules;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ActionExecutor");

        private ActionExecutor(IEnumerable<IServerActionModule> modules)
        {
            this.modules = [];

            foreach (var module in modules)
            {
                if (module == null)
                    continue;

                if (!this.modules.TryAdd(module.Descriptor.Kind, module))
                {
                    throw new InvalidOperationException(
                        $"Duplicate action module for {module.Descriptor.Kind}");
                }
            }
        }

        /// <summary>
        /// 根据<see cref="HeroActionKind"/>执行武器释放,技能释放,佩戴项链
        /// </summary>
        public static ActionExecutor CreateDefault(
            ServerEntityRegistry entities,
            Func<long> clock)
        {
            var modules = ActionModuleCatalog.CreateDefault(entities, clock);
            return new ActionExecutor(modules);
        }

        /// <summary>
        /// 执行一次已通过准入的动作
        /// </summary>
        public bool TryExecute(
            ServerHeroCombatProxy ghost,
            HeroAction request,
            double ratio)
        {
            ghost.SetTransform(
                request.CX, request.CY, request.XR, request.YR,
                request.Dir, request.DX, request.DY);

            int brutality = Math.Clamp(
                request.BrutalityTier, 0, ActionPolicy.MAX_TIER);
            int survival = Math.Clamp(
                request.SurvivalTier, 0, ActionPolicy.MAX_TIER);
            int tactic = Math.Clamp(
                request.TacticTier, 0, ActionPolicy.MAX_TIER);

            if (ghost.Hero.brutalityTier != brutality
                || ghost.Hero.survivalTier != survival
                || ghost.Hero.tacticTier != tactic)
            {
                ghost.SetTiers(brutality, survival, tactic);
            }

            return modules.TryGetValue(request.Kind, out var module)
                && module.TryExecute(ghost, request, ratio);
        }
    }
}
