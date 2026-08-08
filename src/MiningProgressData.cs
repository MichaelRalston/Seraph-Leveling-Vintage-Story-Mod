using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;

namespace SeraphLeveling
{
    /// <summary>
    /// Tracks progress for a specific pickaxe type.
    /// Each pickaxe type has its own increment counter that persists.
    /// </summary>
    public class PickaxeProgressData
    {
        /// <summary>Points accumulated toward the next credit with this pickaxe.</summary>
        public int BlocksInIncrement { get; set; }

        /// <summary>Points needed for the next credit with this pickaxe (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public PickaxeProgressData()
        {
            BlocksInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public PickaxeProgressData Clone()
        {
            return new PickaxeProgressData
            {
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking mining progression with per-pickaxe progress.
    /// Each pickaxe type remembers its own increment counter, encouraging use of many pickaxe types.
    /// </summary>
    public class MiningProgressData: ProgressData<MiningProgressData>, IProgressDataContract<MiningProgressData>
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 150.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-pickaxe progress tracking. Key is pickaxe code (e.g., "game:pickaxe-copper").</summary>
        public Dictionary<string, PickaxeProgressData> PickaxeProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public MiningProgressData()
        {
            TotalCredits = 0;
            PickaxeProgress = new Dictionary<string, PickaxeProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific pickaxe.
        /// New pickaxes start with the configured BaseBlocksPerIncrement.
        /// </summary>
        public PickaxeProgressData GetPickaxeProgress(string pickaxeCode)
        {
            if (!PickaxeProgress.TryGetValue(pickaxeCode, out var progress))
            {
                progress = new PickaxeProgressData
                {
                    BlocksInIncrement = 0,
                    CurrentIncrementSize = SeraphLevelingModSystem.BaseBlocksPerIncrement
                };
                PickaxeProgress[pickaxeCode] = progress;
            }
            return progress;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public MiningProgressData Clone()
        {
            var clone = new MiningProgressData
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                PickaxeProgress = new Dictionary<string, PickaxeProgressData>()
            };
            foreach (var kvp in this.PickaxeProgress)
            {
                clone.PickaxeProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
        public static string GetHeaderString()
        {
            return "SIT";
        }

        public static byte GetVersion() {
            return (byte)4;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(LastActivityDay);

            // Snapshot inner dictionary to avoid concurrent modification
            var pickaxeSnapshot = PickaxeProgress.ToArray();
            writer.Write(pickaxeSnapshot.Length);
            foreach (var pickaxeKvp in pickaxeSnapshot)
            {
                writer.Write(pickaxeKvp.Key); // Pickaxe code
                writer.Write(pickaxeKvp.Value.BlocksInIncrement);
                writer.Write(pickaxeKvp.Value.CurrentIncrementSize);
            }
        }

        public static string SAVE_KEY => "sitMiningProgress";
        public static string Description => "mining";

        public static MiningProgressData ReadVersion(byte version, BinaryReader reader) {
            MiningProgressData progress;
            int pickaxeCount;
            switch (version) {
                case 1:
                    long blocksMined = reader.ReadInt64();

                    // Convert old blocks to credits using legacy formula
                    int legacyLevel = 0;
                    if (blocksMined >= 100)
                    {
                        double discriminant = 1.0 + (8.0 * blocksMined / 100);
                        legacyLevel = (int)((-1.0 + Math.Sqrt(discriminant)) / 2.0);
                    }

                    return new MiningProgressData
                    {
                        TotalCredits = Math.Min(legacyLevel, SeraphLevelingModSystem.MaxMiningSpeedPercent)
                    };
                case 2:
                    int totalCredits = reader.ReadInt32();
                    string currentPickaxeCode = reader.ReadString();
                    int blocksInIncrement = reader.ReadInt32();
                    int currentIncrementSize = reader.ReadInt32();

                    progress = new MiningProgressData
                    {
                        TotalCredits = totalCredits
                    };

                    // Migrate single pickaxe progress if it exists
                    if (!string.IsNullOrEmpty(currentPickaxeCode))
                    {
                        progress.PickaxeProgress[currentPickaxeCode] = new PickaxeProgressData
                        {
                            BlocksInIncrement = blocksInIncrement,
                            CurrentIncrementSize = currentIncrementSize
                        };
                    }
                    return progress;
                case 3:
                    progress = new MiningProgressData
                    {
                        TotalCredits = reader.ReadInt32()
                    };

                    pickaxeCount = reader.ReadInt32();
                    for (int j = 0; j < pickaxeCount; j++)
                    {
                        string pickaxeCode = reader.ReadString();
                        var pickaxeProgress = new PickaxeProgressData
                        {
                            BlocksInIncrement = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.PickaxeProgress[pickaxeCode] = pickaxeProgress;
                    }
                    return progress;
                case 4:
                    progress = new MiningProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };

                    pickaxeCount = reader.ReadInt32();
                    for (int j = 0; j < pickaxeCount; j++)
                    {
                        string pickaxeCode = reader.ReadString();
                        var pickaxeProgress = new PickaxeProgressData
                        {
                            BlocksInIncrement = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.PickaxeProgress[pickaxeCode] = pickaxeProgress;
                    }
                    return progress;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
    }
}