using System;
using System.IO;
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
    public class WalkingProgressData : LeveledTraitProgressData<WalkingProgressData, float>, IProgressDataContract<WalkingProgressData>
    {

        public WalkingProgressData()
        {
            CurrentIncrementSize = 1000; // Base increment size
        }

        public static string GetHeaderString()
        {
            return "SIW";
        }


        public static string SAVE_KEY => "sitWalkingProgress";
        public static string Description => "walking";

        public override int GetMaxCredits(EntityPlayer _) {
            return SeraphLevelingModSystem.MaxWalkingSpeedPercent;
        }
        public override int GetIncrementStep() {
            return SeraphLevelingModSystem.WalkingIncrementStep;
        }
        public override string GetIncrementUnits() {
            return "blocks";
        }
        public override void ApplyBonus(IServerPlayer player) {
            SeraphLevelingModSystem.ApplyWalkingBonusStatic(player, TotalCredits);
        }
        public override void MarkForSave() {
            SeraphLevelingModSystem.pendingWalkingProgressSave = true;
        }
    }
}