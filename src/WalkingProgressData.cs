using Vintagestory.API.Common;
using Vintagestory.API.Server;
using System.Collections.Concurrent;

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
        public override void ApplyBonus(IServerPlayer player) {
            SeraphLevelingModSystem.ApplyWalkingBonusStatic(player, TotalCredits);
        }
    }
}