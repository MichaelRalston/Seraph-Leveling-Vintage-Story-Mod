namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Technical progression.
    /// Binary unlock after repairing translocators.
    /// </summary>
    public class TechnicalProgressData:ProgressData
    {
        /// <summary>Number of translocators repaired.</summary>
        public int TranslocatorsRepaired { get; set; }

        /// <summary>Whether the Technical trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public TechnicalProgressData()
        {
            TranslocatorsRepaired = 0;
            IsUnlocked = false;
        }

        public TechnicalProgressData Clone()
        {
            return new TechnicalProgressData
            {
                TranslocatorsRepaired = this.TranslocatorsRepaired,
                IsUnlocked = this.IsUnlocked
            };
        }
    }
}