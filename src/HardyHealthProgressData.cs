using System;
using System.IO;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Hardy health unlock progression.
    /// One-time burst unlock when reaching mining and armor durability thresholds.
    /// </summary>
    public class HardyHealthProgressData: LockedTraitProgressData<HardyHealthProgressData>, IProgressDataContract<HardyHealthProgressData>
    {
        public static string GetHeaderString() { return "HDH"; }
        public static byte GetVersion() { return 1; }
        public static string SAVE_KEY => "sitHardyHealthProgress";
        public static string Description => "hardy health";
    }
}