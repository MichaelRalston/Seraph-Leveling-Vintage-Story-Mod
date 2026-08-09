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
            return SeraphLevelingModSystem.ApplyWalkingBonusStatic(player, TotalCredits);
        }
    }
}