namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Forager progression.
    /// Tracks wild crop breaking for foraging loot bonuses.
    /// </summary>
    public class ForagerProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Wild crops broken toward the next credit.</summary>
        public int CropsInIncrement { get; set; }

        /// <summary>Crops needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public ForagerProgressData()
        {
            TotalCredits = 0;
            CropsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public ForagerProgressData Clone()
        {
            return new ForagerProgressData
            {
                TotalCredits = this.TotalCredits,
                CropsInIncrement = this.CropsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }
}