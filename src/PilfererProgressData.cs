namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Pilferer progression.
    /// Tracks loot vessels broken for loot bonuses.
    /// </summary>
    public class PilfererProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Points accumulated toward the next credit.</summary>
        public int PointsInIncrement { get; set; }

        /// <summary>Points needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public PilfererProgressData()
        {
            TotalCredits = 0;
            PointsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public PilfererProgressData Clone()
        {
            return new PilfererProgressData
            {
                TotalCredits = this.TotalCredits,
                PointsInIncrement = this.PointsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }
}