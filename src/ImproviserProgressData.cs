namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Improviser unlock progression.
    /// Tracks damage dealt with thrown rocks for sling unlock.
    /// </summary>
    public class ImproviserProgressData:ProgressData
    {
        /// <summary>Total damage dealt with thrown rocks.</summary>
        public float TotalRockDamage { get; set; }

        /// <summary>Whether the Improviser trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public ImproviserProgressData()
        {
            TotalRockDamage = 0;
            IsUnlocked = false;
        }

        public ImproviserProgressData Clone()
        {
            return new ImproviserProgressData
            {
                TotalRockDamage = this.TotalRockDamage,
                IsUnlocked = this.IsUnlocked
            };
        }
    }
}