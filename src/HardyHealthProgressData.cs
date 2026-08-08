namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Hardy health unlock progression.
    /// One-time burst unlock when reaching mining and armor durability thresholds.
    /// </summary>
    public class HardyHealthProgressData:ProgressData
    {
        /// <summary>Whether the Hardy health bonus has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public HardyHealthProgressData()
        {
            IsUnlocked = false;
        }

        public HardyHealthProgressData Clone()
        {
            return new HardyHealthProgressData
            {
                IsUnlocked = this.IsUnlocked
            };
        }
    }
}