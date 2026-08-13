using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SeraphLeveling.Data.Traits
{
    public record class TraitDefinition
    {
        public required string Id { get; init; }
        public required List<(ISaveableAttribute, int)> Attributes { get; init; }
        public List<IRequiredAttribute> Requirements
        {
            get;
            init
            {
                field = value;
                field?.ForEach(req => req.SatisfactionChanged += OnRequirementSatisfactionChanged);
            }
        } = [];

        ~TraitDefinition()
        {
            Requirements?.ForEach(req => req.SatisfactionChanged -= OnRequirementSatisfactionChanged);
        }

        protected void OnRequirementSatisfactionChanged(IServerPlayer player, bool oldSatisfaction, bool newSatisfaction)
        {
            if (newSatisfaction)
            {
                CheckUnlocks(player);
            }
        }

        /// <summary>
        /// Check and apply unlock if all requirements are met.
        /// </summary>
        public void CheckUnlocks(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check prerequisites
            if (Requirements.All(attr => attr.IsSatisfied(player)))
            {
                Attributes.Select(tuple => tuple.Item1).Foreach(attr => attr.Unlock(player, true));
            }
        }

        public virtual TextCommandResult HandleTraitCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();
            Attributes.Select(tuple => tuple.Item1).Foreach(attr => attr.CollectStatus(player, sb));
            if (Requirements.Count > 0)
            {
                sb.AppendLine($"Requirements:");
                Requirements.Foreach(req => req.CollectStatus(player, sb));
            }
            
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }
    }
}
