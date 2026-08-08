using System.Collections.Generic;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Clothier progression.
    /// Tracks unique clothing items worn to unlock sewing kit crafting.
    /// </summary>
    public class ClothierProgressData:ProgressData
    {
        /// <summary>Set of unique clothing item codes that have been worn.</summary>
        public HashSet<string> UniqueClothesWorn { get; set; }

        /// <summary>Whether the sewing kit crafting has been unlocked.</summary>
        public bool SewingKitUnlocked { get; set; }

        public ClothierProgressData()
        {
            UniqueClothesWorn = new HashSet<string>();
            SewingKitUnlocked = false;
        }

        public ClothierProgressData Clone()
        {
            return new ClothierProgressData
            {
                UniqueClothesWorn = new HashSet<string>(this.UniqueClothesWorn),
                SewingKitUnlocked = this.SewingKitUnlocked
            };
        }
    }
}