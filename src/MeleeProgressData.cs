using System.Collections.Generic;

namespace SeraphLeveling
{
    /// <summary>
    /// Tracks progress for a specific weapon type (for melee damage progression).
    /// Each weapon type has its own increment counter that persists.
    /// </summary>
    public class WeaponProgressData
    {
        /// <summary>Damage accumulated toward the next credit with this weapon type.</summary>
        public float DamageInIncrement { get; set; }

        /// <summary>Damage needed for the next credit with this weapon type (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public WeaponProgressData()
        {
            DamageInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public WeaponProgressData Clone()
        {
            return new WeaponProgressData
            {
                DamageInIncrement = this.DamageInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking melee damage progression with per-weapon progress.
    /// Each weapon type remembers its own increment counter, encouraging use of many weapon types.
    /// </summary>
    public class MeleeProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 150.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-weapon progress tracking. Key is weapon type (e.g., "sword", "falx", "spear").</summary>
        public Dictionary<string, WeaponProgressData> WeaponProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public MeleeProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, WeaponProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific weapon type.
        /// New weapons start with the configured BaseDamagePerIncrement.
        /// </summary>
        public WeaponProgressData GetWeaponProgress(string weaponType)
        {
            if (!WeaponProgress.TryGetValue(weaponType, out var progress))
            {
                progress = new WeaponProgressData
                {
                    DamageInIncrement = 0,
                    CurrentIncrementSize = SeraphLevelingModSystem.BaseDamagePerIncrement
                };
                WeaponProgress[weaponType] = progress;
            }
            return progress;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public MeleeProgressData Clone()
        {
            var clone = new MeleeProgressData
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                WeaponProgress = new Dictionary<string, WeaponProgressData>()
            };
            foreach (var kvp in this.WeaponProgress)
            {
                clone.WeaponProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }
}