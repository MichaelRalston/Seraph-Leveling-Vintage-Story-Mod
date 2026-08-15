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
        public required List<IAttributeModifier> Attributes
        {
            get;
            init
            {
                field = value;
                field?.ForEach(mod => mod.ActiveStatusUpdated += OnModifierActiveStatusUpdated);
            }
        }
        public List<IRequirement> Requirements
        {
            get;
            init;
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
        public virtual string DynamicTraitHeaderKey
        {
            get => field ??= $"seraphleveling:trait-{Id}-header"; init;
        }

        /// <summary>
        /// Registers a method to be called every time the unlock status for this attribute changes for a player
        /// </summary>
        public event UnlockChangedDelegate UnlockChanged;

        ~TraitDefinition()
        {
            Attributes?.ForEach(req => req.ActiveStatusUpdated -= OnModifierActiveStatusUpdated);
        }

        protected void OnModifierActiveStatusUpdated(IServerPlayer player, bool _)
        {
            CheckUnlocks(player);
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

            // If the player already has the trait, do nothing
            if (HasVanillaTrait(player.Entity)) return;

            // Check prerequisites
            if (Attributes.All(mod => mod.IsActive(player)))
            {
                Attributes.Select(kvp => kvp.Attribute).Foreach(attr => attr.Unlock(player, true));
                UnlockChanged?.Invoke(player, false, true);
            }
        }

        public virtual TextCommandResult HandleTraitCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();
            Attributes.Select(kvp => kvp.Attribute).Foreach(attr => attr.CollectStatus(player, sb));
            if (Attributes.Any(mod => mod.HasRequirements))
            {
                sb.AppendLine($"Requirements:");
                Attributes.ForEach(mod => mod.CollectRequirementStatus(player, sb));
            }
            
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        public virtual bool HasVanillaTrait(EntityPlayer player)
        {
            return SeraphLevelingModSystem.PlayerHasTrait(player, this);
        }

        protected virtual bool ShouldDisplay(EntityPlayer player)
        {
            return Attributes.Any(a => a.IsActive(player.Player));
        }

        public virtual void BuildTraitText(EntityPlayer player, ref string result)
        {
            bool shouldDisplay = ShouldDisplay(player);
            bool hasVanillaTrait = HasVanillaTrait(player);

            CharacterSystemPatches.ClientApi.Logger.Debug($"[Verdus] Calling BuildTraitText for trait {Id}: shouldDisplay={shouldDisplay}, hasVanillaTrait={hasVanillaTrait}");
            if (shouldDisplay)
            {
                var combinedAttrBonuses = GetCombinedAttributeBonuses(player);
                string headerText = Lang.Get(DynamicTraitHeaderKey);
                string contentText = string.Join(", ", Attributes.Where(mod => mod.IsActive(player.Player)).Select(mod => {
                    string modKey = mod.DynamicAttributeContentsKey.ToLowerInvariant();
                    string retVal = Lang.Get(modKey, combinedAttrBonuses[mod.Attribute].ToString("+0;-#"));
                    CharacterSystemPatches.ClientApi.Logger.Debug($"      [Verdus] Calling BuildTraitText for trait {Id}: attr={mod.Attribute.Id}, key={mod.DynamicAttributeContentsKey}, lang token={retVal}");
                    return retVal == modKey ? null : retVal;
                }).Where(token => token != null));
                string dynamicContents = Lang.Get(CharacterSystemPatches.DYNAMIC_CONTENTS_MESSAGE_KEY, contentText);
                string fullMessage = string.IsNullOrEmpty(contentText) ? "" : Lang.Get(CharacterSystemPatches.FULL_TRAIT_MESSAGE_KEY, headerText, dynamicContents);
                bool messageComplete = fullMessage != CharacterSystemPatches.FULL_TRAIT_MESSAGE_KEY;
                CharacterSystemPatches.ClientApi.Logger.Debug($"   [Verdus] Calling BuildTraitText for trait {Id}: headerText={headerText}");
                CharacterSystemPatches.ClientApi.Logger.Debug($"   [Verdus] Calling BuildTraitText for trait {Id}: contentText={contentText}");
                CharacterSystemPatches.ClientApi.Logger.Debug($"   [Verdus] Calling BuildTraitText for trait {Id}: dynamicContents={dynamicContents}");
                CharacterSystemPatches.ClientApi.Logger.Debug($"   [Verdus] Calling BuildTraitText for trait {Id}: fullMessage={fullMessage}");
                if (messageComplete)
                {
                    if (hasVanillaTrait)
                    {
                        result = CharacterSystemPatches.ReplaceVanillaTraitLine(result, Id, fullMessage);
                    }
                    else
                    {
                        result = result + "\n" + fullMessage;
                    }
                }
            }
        }

        protected virtual Dictionary<ISaveableAttribute, int> GetCombinedAttributeBonuses(EntityPlayer player)
        {
            Dictionary<ISaveableAttribute, int> retVal = [];
            bool hasVanillaTrait = HasVanillaTrait(player);
            foreach (var kvp in Attributes)
            {
                if (kvp.Attribute is ILeveledAttributeModifierDefinition leveledAttr)
                {
                    retVal[leveledAttr] = leveledAttr.GetBonusPercent(player);
                    if (hasVanillaTrait)
                    {
                        retVal[leveledAttr] += kvp.ModifierValue;
                    }
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
            return Attributes.Select(kvp => kvp.Attribute.GetLocalizedTraitTextParam(player)).Where(param => param != null).ToArray();
        }
    }
}
