namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Claustrophobic removal progression (Hunter class).
    /// Removes the Claustrophobic negative trait after reaching mining threshold.
    /// </summary>
    public class ClaustrophobicRemovalProgressData
    {
        /// <summary>Whether the Claustrophobic trait has been removed.</summary>
        public bool IsRemoved { get; set; }

        public ClaustrophobicRemovalProgressData()
        {
            IsRemoved = false;
        }

        public ClaustrophobicRemovalProgressData Clone()
        {
            return new ClaustrophobicRemovalProgressData
            {
                IsRemoved = this.IsRemoved
            };
        }
    }
}