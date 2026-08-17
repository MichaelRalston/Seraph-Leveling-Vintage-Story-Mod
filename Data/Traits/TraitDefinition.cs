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
        private static readonly List<string> DebugTraits = ["frail"];

        private void DebugLog(bool client, bool server, string message)
        {
            if (DebugTraits.Contains(Id))
            {
                if (client)
                {
                    CharacterSystemPatches.ClientApi.Logger.Debug(message);
                }
                if (server)
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug(message);
                }
            }
        }

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
            get => field ??= $"trait-{Id}"; init;
        }

        /// <summary>
        /// Registers a method to be called every time the unlock status for this attribute changes for a player
        /// </summary>
        public event UnlockChangedDelegate UnlockChanged;

        ~TraitDefinition()
        {
            Attributes?.ForEach(req => req.ActiveStatusUpdated -= OnModifierActiveStatusUpdated);
        }

        protected void OnModifierActiveStatusUpdated(IServerPlayer player, bool newValue)
        {
            DebugLog(false, true, $"[Verdus] Calling OnModifierActiveStatusUpdated for trait {Id} with newValue={newValue}");
            CheckUnlocks(player);
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
                DebugLog(false, true, $"[Verdus] All attributes active for trait {Id}!");
                Attributes.Select(kvp => kvp.Attribute).Foreach(attr => attr.Unlock(player, true));
                UnlockChanged?.Invoke(player, false, true);
            }
            else
            {
                DebugLog(false, true, $"[Verdus] Not all attributes active for trait {Id}");
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

        protected virtual bool HasEarnedProgress(EntityPlayer player)
        {
            return Attributes.Any(mod => mod.Attribute.ShouldDisplay(player));
        }

        protected virtual bool ShouldDisplay(EntityPlayer player)
        {
            return Attributes.Any(a => a.IsActive(player.Player));
        }

        public virtual void BuildTraitText(EntityPlayer player, ref string result)
        {
            bool shouldDisplay = ShouldDisplay(player);
            bool hasEarnedProgress = HasEarnedProgress(player);
            bool hasVanillaTrait = HasVanillaTrait(player);

            DebugLog(true, false, $"[Verdus] Calling BuildTraitText for trait {Id}: shouldDisplay={shouldDisplay}, hasVanillaTrait={hasVanillaTrait}");
            if (shouldDisplay)
            {
                var combinedAttrBonuses = GetCombinedAttributeBonuses(player);
                string headerText = Lang.Get(DynamicTraitHeaderKey);
                string contentText = string.Join(", ", Attributes.Where(mod => mod.IsActive(player.Player)).Select(mod => {
                    string modKey = mod.DynamicAttributeContentsKey.ToLowerInvariant();
                    if (combinedAttrBonuses.TryGetValue(mod.Attribute, out string combinedBonus))
                    {
                        string retVal = Lang.Get(modKey, combinedBonus);
                        DebugLog(true, false, $"      [Verdus] Calling BuildTraitText for trait {Id}: attr={mod.Attribute.Id}, key={mod.DynamicAttributeContentsKey}, lang token={retVal}");
                        return retVal == modKey ? null : retVal;
                    }
                    else
                    {
                        DebugLog(true, false, $"      [Verdus] Failed to get combined bonus for attribute {mod.Attribute.Id}");
                        return null;
                    }
                }).Where(token => token != null));
                string fullMessage = string.IsNullOrEmpty(contentText) ? "" : Lang.Get(CharacterSystemPatches.FULL_TRAIT_MESSAGE_KEY, headerText, contentText);
                bool messageComplete = fullMessage != CharacterSystemPatches.FULL_TRAIT_MESSAGE_KEY;
                DebugLog(true, false, $"   [Verdus] Calling BuildTraitText for trait {Id}: headerText={headerText}");
                DebugLog(true, false, $"   [Verdus] Calling BuildTraitText for trait {Id}: contentText={contentText}");
                DebugLog(true, false, $"   [Verdus] Calling BuildTraitText for trait {Id}: fullMessage={fullMessage}");
                if (messageComplete)
                {
                    if (hasVanillaTrait)
                    {
                        result = CharacterSystemPatches.ReplaceVanillaTraitLine(result, Id, fullMessage);
                    }
                    else if (CharacterSystemPatches.HasNoTraits(result))
                    {
                        result = fullMessage;
                    }
                    else
                    {
                        result = result + "\n" + fullMessage;
                    }
                }
            }
            else if (hasVanillaTrait && hasEarnedProgress)
            {
                result = CharacterSystemPatches.RemoveVanillaTraitLine(result, Id);
            }
        }

        protected virtual Dictionary<ISaveableAttribute, string> GetCombinedAttributeBonuses(EntityPlayer player)
        {
            Dictionary<ISaveableAttribute, string> retVal = [];
            foreach (var mod in Attributes)
            {
                if (mod.Attribute is ILeveledAttributeModifierDefinition leveledAttr)
                {
                    int attrVal = leveledAttr.GetBonusPercent(player);
                    if (SeraphLevelingModSystem.TraitsForAttributes.TryGetValue(leveledAttr.Id, out var traitModList))
                    {
                        foreach (var (traitDef, modVal) in traitModList)
                        {
                            if (SeraphLevelingModSystem.PlayerHasTrait(player, traitDef))
                            {
                                attrVal += modVal;
                            }
                        }
                    }
                    retVal[leveledAttr] = attrVal.ToString("+0;-#");
                }
                else if (mod.Attribute is MaxHealthUnlockedAttributeModifierDefinition maxHealthAttr)
                {
                    // Max health modifier values are always displayed separately, without regard for other max health modifiers
                    float attrVal = maxHealthAttr.ModifierAmount;
                    retVal[maxHealthAttr] = attrVal.ToString("+0;-#");
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
