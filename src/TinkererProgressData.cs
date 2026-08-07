namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Tinkerer unlock progression.
    /// Unlocks after obtaining Technical trait and reaching Precise damage threshold.
    /// </summary>
    public class TinkererProgressData
    {
        /// <summary>Whether the Tinkerer trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public TinkererProgressData()
        {
            IsUnlocked = false;
        }

        public TinkererProgressData Clone()
        {
            return new TinkererProgressData
            {
                IsUnlocked = this.IsUnlocked
            };
        }
    }
}