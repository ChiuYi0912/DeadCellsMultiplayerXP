using System;
using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.Data.Ghosts;

namespace DeadCellsMultiplayerX.Common.Simulation.Actions
{
    public sealed class ActionRegistry
    {
        private readonly Dictionary<HeroActionKind, ActionDescriptor> descriptors = [];

        public ActionRegistry(IEnumerable<ActionDescriptor> descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            foreach (var descriptor in descriptors)
            {
                if (descriptor == null)
                    continue;

                if (!this.descriptors.TryAdd(descriptor.Kind, descriptor))
                {
                    throw new InvalidOperationException(
                        $"Duplicate action descriptor for {descriptor.Kind}");
                }
            }
        }

        public IReadOnlyCollection<ActionDescriptor> All => descriptors.Values;

        public bool TryGet(HeroActionKind kind, out ActionDescriptor descriptor)
            => descriptors.TryGetValue(kind, out descriptor!);

        public ActionDescriptor? Find(HeroActionKind kind)
            => descriptors.TryGetValue(kind, out var descriptor)
                ? descriptor
                : null;
    }
}
