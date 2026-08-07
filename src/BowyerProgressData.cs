namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Bowyer unlock progression.
    /// Tracks bow damage with simple bow and longbow for unlock.
    /// </summary>
    public class BowyerProgressData
    {
        /// <summary>Total damage dealt with simple bow or longbow.</summary>
        public float TotalBowDamage { get; set; }

        /// <summary>Whether the Bowyer trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public BowyerProgressData()
        {
            TotalBowDamage = 0;
            IsUnlocked = false;
        }

        public BowyerProgressData Clone()
        {
            return new BowyerProgressData
            {
                TotalBowDamage = this.TotalBowDamage,
                IsUnlocked = this.IsUnlocked
            };
        }
    }
}