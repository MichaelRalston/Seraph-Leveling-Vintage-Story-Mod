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

        public void CheckUnlock(IServerPlayer player)
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
