using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Collections.Concurrent;

namespace SeraphLeveling.Data.Legacy
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
    public class MeleeProgressData:ProgressData<MeleeProgressData>, IProgressDataContract<MeleeProgressData>
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
        public static string GetHeaderString()
        {
            return "SIM";
        }
        public static byte GetVersion()
        {
            return (byte)2;
        }
        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(TotalCredits);
            writer.Write(LastActivityDay);

            // Write per-weapon progress dictionary
            var weaponSnapshot = WeaponProgress.ToArray();
            writer.Write(weaponSnapshot.Length);
            foreach (var weaponKvp in weaponSnapshot)
            {
                writer.Write(weaponKvp.Key); // Weapon type
                writer.Write(weaponKvp.Value.DamageInIncrement);
                writer.Write(weaponKvp.Value.CurrentIncrementSize);
            }
        }
        public static string SAVE_KEY => "sitMeleeProgress";
        public static string Description => "melee";
        public static MeleeProgressData ReadVersion(byte version, BinaryReader reader)
        {
            MeleeProgressData progress;
            int weaponCount;
            switch (version)
            {
                case 1:
                    progress = new MeleeProgressData
                    {
                        TotalCredits = reader.ReadInt32()
                    };

                    weaponCount = reader.ReadInt32();
                    for (int j = 0; j < weaponCount; j++)
                    {
                        string weaponType = reader.ReadString();
                        var weaponProgress = new WeaponProgressData
                        {
                            DamageInIncrement = reader.ReadSingle(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.WeaponProgress[weaponType] = weaponProgress;
                    }
                    return progress;
                case 2:
                    progress = new MeleeProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };

                    weaponCount = reader.ReadInt32();
                    for (int j = 0; j < weaponCount; j++)
                    {
                        string weaponType = reader.ReadString();
                        var weaponProgress = new WeaponProgressData
                        {
                            DamageInIncrement = reader.ReadSingle(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.WeaponProgress[weaponType] = weaponProgress;
                    }
                    return progress;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingMeleeProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, MeleeProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.MeleeProgress;
        }
    }
}
