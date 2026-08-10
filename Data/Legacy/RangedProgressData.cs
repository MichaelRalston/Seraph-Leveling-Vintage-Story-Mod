using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace SeraphLeveling.Data.Legacy
{
    /// <summary>
    /// Tracks progress for a specific ranged weapon combination (for ranged damage progression).
    /// Each weapon combination (bow+arrow) has its own increment counter that persists.
    /// </summary>
    public class RangedWeaponProgressData
    {
        /// <summary>Damage accumulated toward the next credit with this weapon combination.</summary>
        public float DamageInIncrement { get; set; }

        /// <summary>Damage needed for the next credit with this weapon combination (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public RangedWeaponProgressData()
        {
            DamageInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }
        public RangedWeaponProgressData(BinaryReader reader)
        {
            DamageInIncrement = reader.ReadSingle();
            CurrentIncrementSize = reader.ReadInt32();
        }
        public RangedWeaponProgressData Clone()
        {
            return new RangedWeaponProgressData
            {
                DamageInIncrement = this.DamageInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking ranged damage progression with per-weapon progress.
    /// Each weapon combination remembers its own increment counter, encouraging use of many weapon types.
    /// </summary>
    public class RangedProgressData : ProgressData<RangedProgressData>, IProgressDataContract<RangedProgressData>
    {
        /// <summary>Total credits earned (each credit = 1% bonus to damage/accuracy/distance). Max 130.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-weapon progress tracking. Key is weapon combination (e.g., "bow-long+arrow-copper").</summary>
        public Dictionary<string, RangedWeaponProgressData> WeaponProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public RangedProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, RangedWeaponProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific weapon combination.
        /// New weapons start with the configured BaseRangedDamagePerIncrement.
        /// </summary>
        public RangedWeaponProgressData GetWeaponProgress(string weaponCombo)
        {
            if (!WeaponProgress.TryGetValue(weaponCombo, out var progress))
            {
                progress = new RangedWeaponProgressData
                {
                    DamageInIncrement = 0,
                    CurrentIncrementSize = SeraphLevelingModSystem.BaseRangedDamagePerIncrement
                };
                WeaponProgress[weaponCombo] = progress;
            }
            return progress;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public RangedProgressData Clone()
        {
            var clone = new RangedProgressData
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                WeaponProgress = new Dictionary<string, RangedWeaponProgressData>()
            };
            foreach (var kvp in this.WeaponProgress)
            {
                clone.WeaponProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
        public static string GetHeaderString() {
            return "SIR";
        }
        public static byte GetVersion() {
            return (byte)2;
        }
        public static string SAVE_KEY => "sitRangedProgress";
        public static string Description => "ranged";
        public static RangedProgressData ReadVersion(byte version, BinaryReader reader)
        {
            RangedProgressData progress;
            int weaponCount;
            switch (version) {
                case 1:
                    progress = new RangedProgressData
                    {
                        TotalCredits = reader.ReadInt32()
                    };

                    weaponCount = reader.ReadInt32();
                    for (int j = 0; j < weaponCount; j++)
                    {
                        string weaponCombo = reader.ReadString();
                        var weaponProgress = new RangedWeaponProgressData(reader);
                        progress.WeaponProgress[weaponCombo] = weaponProgress;
                    }
                    return progress;
                case 2:
                    progress = new RangedProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };

                    weaponCount = reader.ReadInt32();
                    for (int j = 0; j < weaponCount; j++)
                    {
                        string weaponCombo = reader.ReadString();
                        var weaponProgress = new RangedWeaponProgressData
                        {
                            DamageInIncrement = reader.ReadSingle(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.WeaponProgress[weaponCombo] = weaponProgress;
                    }
                    return progress;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(LastActivityDay);

            // Write per-weapon progress dictionary
            var weaponSnapshot = WeaponProgress.ToArray();
            writer.Write(weaponSnapshot.Length);
            foreach (var weaponKvp in weaponSnapshot)
            {
                writer.Write(weaponKvp.Key); // Weapon combo
                writer.Write(weaponKvp.Value.DamageInIncrement);
                writer.Write(weaponKvp.Value.CurrentIncrementSize);
            }
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingRangedProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, RangedProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.RangedProgress;
        }
    }
}
