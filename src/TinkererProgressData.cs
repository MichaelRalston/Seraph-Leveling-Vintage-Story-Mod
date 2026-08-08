namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Tinkerer unlock progression.
    /// Unlocks after obtaining Technical trait and reaching Precise damage threshold.
    /// </summary>
    public class TinkererProgressData: LockedTraitProgressData<TinkererProgressData>, IProgressDataContract<TinkererProgressData>
    {
        public static string GetHeaderString() { return "TNK"; }
        public static byte GetVersion() { return 1; }
        public static string SAVE_KEY => "sitTinkererProgress";
        public static string Description => "tinkerer";
    }
}