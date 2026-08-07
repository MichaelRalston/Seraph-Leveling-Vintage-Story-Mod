namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Furtive progression.
    /// Tracks blocks of sneaking for animal detection range reduction.
    /// </summary>
    public class FurtiveProgressData
    {
        /// <summary>Total credits earned (each credit = -1% animal detection range). Max 35.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Sneaking blocks accumulated toward the next credit.</summary>
        public float BlocksInIncrement { get; set; }

        /// <summary>Blocks needed for the next credit (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public FurtiveProgressData()
        {
            TotalCredits = 0;
            BlocksInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
            LastActivityDay = 0;
        }

        public FurtiveProgressData Clone()
        {
            return new FurtiveProgressData
            {
                TotalCredits = this.TotalCredits,
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }
}