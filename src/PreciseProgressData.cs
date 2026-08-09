using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace SeraphLeveling
{
    /// <summary>
    /// Tracks progress for a specific weapon type (for Precise damage to mechanicals).
    /// Each weapon type has its own increment counter that persists.
    /// </summary>
    public class PreciseWeaponProgressData
    {
        /// <summary>Damage accumulated toward the next credit with this weapon type.</summary>
        public float DamageInIncrement { get; set; }

        /// <summary>Damage needed for the next credit with this weapon type (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public PreciseWeaponProgressData()
        {
            DamageInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public PreciseWeaponProgressData Clone()
        {
            return new PreciseWeaponProgressData
            {
                DamageInIncrement = this.DamageInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Precise progression.
    /// Tracks damage dealt to mechanical creatures for damage bonus.
    /// </summary>
    public class PreciseProgressData: ProgressData<PreciseProgressData>, IProgressDataContract<PreciseProgressData>
    {
        /// <summary>Total credits earned (each credit = +1% damage to mechanicals). Max 30.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-weapon progress tracking. Key is weapon type (e.g., "sword-copper", "spear-iron").</summary>
        public Dictionary<string, PreciseWeaponProgressData> WeaponProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public PreciseProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, PreciseWeaponProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific weapon type.
        /// </summary>
        public PreciseWeaponProgressData GetWeaponProgress(string weaponType)
        {
            if (!WeaponProgress.TryGetValue(weaponType, out var progress))
            {
                progress = new PreciseWeaponProgressData
                {
                    DamageInIncrement = 0,
                    CurrentIncrementSize = SeraphLevelingModSystem.BasePreciseDamagePerIncrement
                };
                WeaponProgress[weaponType] = progress;
            }
            return progress;
        }

        public PreciseProgressData Clone()
        {
            var clone = new PreciseProgressData
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                WeaponProgress = new Dictionary<string, PreciseWeaponProgressData>()
            };
            foreach (var kvp in this.WeaponProgress)
            {
                clone.WeaponProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
        public static string GetHeaderString()
        {
            return "PRC";
        }

        public static byte GetVersion() {
            return (byte)2;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(LastActivityDay);

            // Write weapon progress
            var weaponSnapshot = WeaponProgress.ToArray();
            writer.Write(weaponSnapshot.Length);
            foreach (var weaponKvp in weaponSnapshot)
            {
                writer.Write(weaponKvp.Key);
                writer.Write(weaponKvp.Value.DamageInIncrement);
                writer.Write(weaponKvp.Value.CurrentIncrementSize);
            }
        }

        public static string SAVE_KEY => "sitPreciseProgress";
        public static string Description => "precise";

        public static PreciseProgressData ReadVersion(byte version, BinaryReader reader) {
            PreciseProgressData progress;
            int weaponCount;
            switch (version) {
                case 1:
                    progress = new PreciseProgressData
                    {
                        TotalCredits = reader.ReadInt32()
                    };

                    weaponCount = reader.ReadInt32();
                    for (int j = 0; j < weaponCount; j++)
                    {
                        string weaponKey = reader.ReadString();
                        var weaponProgress = new PreciseWeaponProgressData
                        {
                            DamageInIncrement = reader.ReadSingle(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.WeaponProgress[weaponKey] = weaponProgress;
                    }
                    return progress;
                case 2:
                    progress = new PreciseProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };

                    weaponCount = reader.ReadInt32();
                    for (int j = 0; j < weaponCount; j++)
                    {
                        string weaponKey = reader.ReadString();
                        var weaponProgress = new PreciseWeaponProgressData
                        {
                            DamageInIncrement = reader.ReadSingle(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.WeaponProgress[weaponKey] = weaponProgress;
                    }
                    return progress;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingPreciseProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, PreciseProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.PreciseProgress;
        }
    }
}