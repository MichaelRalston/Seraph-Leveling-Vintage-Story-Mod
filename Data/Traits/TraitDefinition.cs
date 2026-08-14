using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SeraphLeveling.Data.Attributes;
using SeraphLeveling.Patches;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SeraphLeveling.Data.Traits
{
    public record class TraitDefinition
    {
        public required string Id { get; init; }
        public required Dictionary<ISaveableAttribute, int> Attributes { get; init; }
        public List<IRequiredAttribute> Requirements
        {
            get;
            init
            {
                field = value;
                field?.ForEach(req => req.SatisfactionChanged += OnRequirementSatisfactionChanged);
            }
        } = [];
        public bool MergeWithVanilla { get; init; } = false;
        public virtual string PlainTraitNameKey
        {
            get => field ??= $"seraphleveling:trait-sit{Id}mastery"; init;
        }
        public virtual string DynamicTraitTextKey
        {
            get => field ??= $"seraphleveling:trait-{Id}-dynamic"; init;
        }

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
                Attributes.Select(kvp => kvp.Key).Foreach(attr => attr.Unlock(player, true));
            }
        }

        public virtual TextCommandResult HandleTraitCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();
            Attributes.Select(kvp => kvp.Key).Foreach(attr => attr.CollectStatus(player, sb));
            if (Requirements.Count > 0)
            {
                sb.AppendLine($"Requirements:");
                Requirements.Foreach(req => req.CollectStatus(player, sb));
            }
            
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        protected virtual bool HasVanillaTrait(EntityPlayer player)
        {
            return SeraphLevelingModSystem.PlayerHasTrait(player, this);
        }

        protected virtual bool ShouldDisplay(EntityPlayer player)
        {
            return Attributes.Any(a => a.Key.ShouldDisplay(player)) && (MergeWithVanilla || !HasVanillaTrait(player));
        }

        private int GetVanillaValue(ILeveledAttributeModifierDefinition attr)
        {
            return Attributes.TryGetValue(attr, out int val) ? val : 0;
        }

        public virtual void AppendTraitText(EntityPlayer player, ref string result)
        {
            if (ShouldDisplay(player))
            {
                string plainTraitName = Lang.Get(PlainTraitNameKey);
                string dynamicTraitText = BuildLocalizedTraitLine(player);
                bool hasVanillaTrait = SeraphLevelingModSystem.PlayerHasTrait(player, this);
                if (MergeWithVanilla && hasVanillaTrait)
                {
                    // Class already has vanilla trait - update the inline walk speed value (locale-aware).
                    var combinedBonuses = GetCombinedAttributeBonuses(player);
                    foreach (var key in combinedBonuses.Keys)
                    {
                        result = CharacterSystemPatches.ReplaceVanillaCharAttribute(result, key.StatName, 0.01D * GetVanillaValue(key), combinedBonuses[key]);
                        result = CharacterSystemPatches.RemoveOrphanTraitName(result, plainTraitName);
                    }
                }
                else if (CharacterSystemPatches.HasNoTraits(result))
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic trait text
                    result = dynamicTraitText;
                }
                else if (CharacterSystemPatches.ContainsOrphanTraitName(result, plainTraitName))
                {
                    // We have our dynamic trait but no vanilla trait - replace orphan plain name with dynamic version
                    result = CharacterSystemPatches.ReplaceOrphanTraitName(result, plainTraitName, dynamicTraitText);
                }
                else
                {
                    // Has other traits but no vanilla trait at all - append our dynamic trait text
                    result = result + "\n" + dynamicTraitText;
                }
            }
        }

        protected virtual Dictionary<ILeveledAttributeModifierDefinition, int> GetCombinedAttributeBonuses(EntityPlayer player)
        {
            Dictionary<ILeveledAttributeModifierDefinition, int> retVal = [];
            foreach (var kvp in Attributes)
            {
                if (kvp.Key is ILeveledAttributeModifierDefinition leveledAttr)
                {
                    retVal[leveledAttr] = kvp.Value + leveledAttr.GetBonusPercent(player);
                }
            }
            return retVal;
        }

        /// <summary>
        /// Builds a fully localized trait line `{traitName} <font opacity="0.6">({desc})</font>`
        /// using vanilla's `traitwithattributes` template, with the trait name pulled from the
        /// vanilla `trait-{code}` lang key (so it shows up in the player's locale: "Hardy" in EN,
        /// "Robuste" in FR, etc.) and the description from one of our `seraphleveling:` lang
        /// values. The seraphleveling lang values store only the inner description text — the
        /// trait label and font wrapper come from this helper.
        /// </summary>
        protected virtual string BuildLocalizedTraitLine(EntityPlayer player)
        {
            string traitName = Lang.Get("trait-" + Id);
            string desc = Lang.Get(DynamicTraitTextKey, GetLocalizedTraitTextParams(player));
            return Lang.Get("traitwithattributes", traitName, desc);
        }

        protected virtual object[] GetLocalizedTraitTextParams(EntityPlayer player)
        {
            return Attributes.Select(kvp => kvp.Key.GetLocalizedTraitTextParam(player)).Where(param => param != null).ToArray();
        }
    }
}
