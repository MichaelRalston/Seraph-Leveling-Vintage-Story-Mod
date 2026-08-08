namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Merciless unlock progression.
    /// Unlocks after reaching armor durability and melee damage thresholds.
    /// </summary>
    public class MercilessProgressData:ProgressData
    {
        /// <summary>Whether the Merciless trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public MercilessProgressData()
        {
            IsUnlocked = false;
        }

        public MercilessProgressData Clone()
        {
            return new MercilessProgressData
            {
                IsUnlocked = this.IsUnlocked
            };
        }
    }
}