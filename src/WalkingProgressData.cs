using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking walking speed progression.
    /// Simpler than other progression systems since walking has no "tools".
    /// </summary>
    public class WalkingProgressData : LeveledTraitProgressData<WalkingProgressData, float>, IProgressDataContract<WalkingProgressData>, ILeveledTraitContract<WalkingProgressData>
    {

        public WalkingProgressData()
        {
            CurrentIncrementSize = SeraphLevelingModSystem.BaseBlocksWalkedPerIncrement;
        }

        public static string GetHeaderString()
        {
            return "SIW";
        }


        public static string SAVE_KEY => "sitWalkingProgress";
        public static string Description => "walking";
        public static string Name => "Walking";
        public static string Stat => "% speed";
        public static string LongDescription => "walking speed";
        public static int GlobalMax
        {
            get => SeraphLevelingModSystem.MaxWalkingSpeedPercent;
            set => SeraphLevelingModSystem.MaxWalkingSpeedPercent = value;
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingWalkingProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, WalkingProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.WalkingProgress;
        }

        public override int GetMaxCredits(EntityPlayer _) {
            return SeraphLevelingModSystem.MaxWalkingSpeedPercent;
        }
        public override int GetIncrementStep() {
            return SeraphLevelingModSystem.WalkingIncrementStep;
        }
        public override int GetBaseIncrement() {
            return SeraphLevelingModSystem.BaseBlocksWalkedPerIncrement;
        }
        public override string GetIncrementUnits() {
            return "blocks";
        }
        public override int CalculateBonus(EntityPlayer entity) {
            bool hasFleetfooted = entity != null && SeraphLevelingModSystem.PlayerHasVanillaFleetfootedStatic(entity);
            int vanillaBonus = hasFleetfooted ? SeraphLevelingModSystem.VANILLA_FLEETFOOTED_WALK_BONUS : 0;
            int earnableBonus = Math.Max(0, GetMaxCredits(entity) - vanillaBonus);
            return Math.Min(TotalCredits, earnableBonus);
        }
        public override int ApplyBonus(IServerPlayer player) {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = SeraphLevelingModSystem.GetCachedTraits(player.PlayerUID);
            bool hasVanillaFleetfooted = cache?.HasFleetfooted ?? SeraphLevelingModSystem.PlayerHasVanillaFleetfootedStatic(player.Entity);
            int vanillaFleetfootedBonus = hasVanillaFleetfooted ? SeraphLevelingModSystem.VANILLA_FLEETFOOTED_WALK_BONUS : 0;

            // Calculate raw bonus from level (1% per level)
            float rawBonus = TotalCredits * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxWalkingSpeedPercent
            float maxEarnableBonus = (GetMaxCredits(player.Entity) - vanillaFleetfootedBonus) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));
            int bonusPercent = (int)(bonus * 100);

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("walkspeed", SeraphLevelingModSystem.WALKING_STAT_CODE, bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_BONUS, -1);

            bool valuesChanged = (oldLevel != TotalCredits) || (oldBonus != bonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, TotalCredits);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_WALKING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaFleetfooted", hasVanillaFleetfooted);

                // Add our trait to extraTraits only if player doesn't already have Fleetfooted
                SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, SeraphLevelingModSystem.WALKING_TRAIT_CODE, TotalCredits > 0 && !hasVanillaFleetfooted);

                watchedAttrs.MarkPathDirty(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL);
            }

            return bonusPercent;
        }
    }
}
