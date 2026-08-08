namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Resourceful progression.
    /// Tracks animal harvesting for loot and speed bonuses.
    /// </summary>
    public class ResourcefulProgressData:ProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Animals harvested toward the next credit.</summary>
        public int AnimalsInIncrement { get; set; }

        /// <summary>Animals needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public ResourcefulProgressData()
        {
            TotalCredits = 0;
            AnimalsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public ResourcefulProgressData Clone()
        {
            return new ResourcefulProgressData
            {
                TotalCredits = this.TotalCredits,
                AnimalsInIncrement = this.AnimalsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }
}