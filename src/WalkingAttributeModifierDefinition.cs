using System;
using Vintagestory.API.Server;

namespace SeraphLeveling
{
    public record class WalkingAttributeModifierDefinition : LeveledAttributeModifierDefinition
    {
        public override int ApplyBonus(IServerPlayer player, LeveledAttributeModifierProgressData progressData)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = SeraphLevelingModSystem.GetCachedTraits(player.PlayerUID);
            bool hasVanillaFleetfooted = cache?.HasFleetfooted ?? SeraphLevelingModSystem.PlayerHasVanillaFleetfootedStatic(player.Entity);
            int vanillaFleetfootedBonus = hasVanillaFleetfooted ? SeraphLevelingModSystem.VANILLA_FLEETFOOTED_WALK_BONUS : 0;

            // Calculate raw bonus from level (1% per level)
            float rawBonus = progressData.TotalCredits * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxWalkingSpeedPercent
            float maxEarnableBonus = (AttributeModifierDefinitions.WalkingSpeed.GlobalMaxCredits - vanillaFleetfootedBonus) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));
            int bonusPercent = (int)(bonus * 100);

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("walkspeed", SeraphLevelingModSystem.WALKING_STAT_CODE, bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_BONUS, -1);

            bool valuesChanged = (oldLevel != progressData.TotalCredits) || (oldBonus != bonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, progressData.TotalCredits);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_WALKING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaFleetfooted", hasVanillaFleetfooted);

                // Add our trait to extraTraits only if player doesn't already have Fleetfooted
                SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, SeraphLevelingModSystem.WALKING_TRAIT_CODE, progressData.TotalCredits > 0 && !hasVanillaFleetfooted);

                watchedAttrs.MarkPathDirty(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL);
            }

            return bonusPercent;
        }
    }
}
