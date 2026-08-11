using System;
using System.Collections.Generic;
using System.Linq;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SeraphLeveling.Data.Traits
{
    public record class TraitDefinition
    {
        public required string Id { get; init; }
        public required List<(ISaveableAttribute, int)> Attributes { get; init; }
        public List<IRequiredAttribute> Requirements { get; init; } = [];

        /// <summary>
        /// Check and apply unlock if all requirements are met.
        /// </summary>
        public void CheckUnlocks(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check prerequisites
            if (Requirements.All(attr => attr.IsMet(player)))
            {
                Attributes.Select(tuple => tuple.Item1).Foreach(attr => attr.Unlock(player, true));
            }
        }
    }
}
