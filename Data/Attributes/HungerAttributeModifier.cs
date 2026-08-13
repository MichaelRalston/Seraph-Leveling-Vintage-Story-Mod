using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public record class HungerAttributeModifierDefinition : LeveledPartialAttributeModifierDefinition
    {
        public override int GetMaxCredits(EntityPlayer entity)
        {
            bool hasRavenous = entity != null && SeraphLevelingModSystem.PlayerHasVanillaRavenousStatic(entity);
            int ravenousPenalty = hasRavenous ? SeraphLevelingModSystem.VANILLA_RAVENOUS_HUNGER_PENALTY : 0;
            // MaxHungerReductionPercent represents how much a normal player needs to reduce
            // Ravenous players need that PLUS their penalty to reach the same target
            return GlobalMaxCredits + ravenousPenalty;

        }
        public override int ApplyBonus(IServerPlayer player, LeveledPartialAttributeModifierProgressData progressData)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = SeraphLevelingModSystem.GetCachedTraits(player.PlayerUID);
            bool hasVanillaRavenous = cache?.HasRavenous ?? SeraphLevelingModSystem.PlayerHasVanillaRavenousStatic(player.Entity);

            // Calculate max credits this player can earn
            int maxCredits = GetMaxCredits(player.Entity);

            // Calculate bonus from level (1% per level, capped at player's max)
            int cappedLevel = Math.Min(progressData.TotalCredits, maxCredits);
            float bonus = cappedLevel * 0.01f;
            int bonusPercent = (int)(bonus * 100);

            // Calculate remaining Ravenous penalty (0 when fully cancelled at level 30)
            int ravenousRemaining = hasVanillaRavenous ? SeraphLevelingModSystem.CalculateRemainingPenalty(SeraphLevelingModSystem.VANILLA_RAVENOUS_HUNGER_PENALTY, progressData.TotalCredits) : 0;

            // Always apply stats (they're not persistent)
            // Set the hunger rate stat - this value is ADDED to the base (1.0)
            // We want to REDUCE hunger rate, so we use a negative value
            player.Entity.Stats.Set("hungerrate", SeraphLevelingModSystem.HUNGER_STAT_CODE, -bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_HUNGER_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_HUNGER_BONUS, -1);

            bool valuesChanged = (oldLevel != progressData.TotalCredits) || (oldBonus != bonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_HUNGER_LEVEL, progressData.TotalCredits);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_HUNGER_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaRavenous", hasVanillaRavenous);
                watchedAttrs.SetInt("sitMaxHungerCredits", maxCredits);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_RAVENOUS_REMAINING, ravenousRemaining);

                // Add our trait to extraTraits (hunger mastery is unique, doesn't replace a vanilla trait)
                SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, SeraphLevelingModSystem.HUNGER_TRAIT_CODE, progressData.TotalCredits > 0);

                // Only call MarkPathDirty once (batched update)
                watchedAttrs.MarkPathDirty(SeraphLevelingModSystem.WATCHED_HUNGER_LEVEL);
            }

            return bonusPercent;
        }
    }
}
