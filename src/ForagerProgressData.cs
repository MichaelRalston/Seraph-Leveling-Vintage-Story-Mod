using System;
using System.IO;
using System.Collections.Concurrent;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Forager progression.
    /// Tracks wild crop breaking for foraging loot bonuses.
    /// </summary>
    public class ForagerProgressData: ProgressData<ForagerProgressData>, IProgressDataContract<ForagerProgressData>
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Wild crops broken toward the next credit.</summary>
        public int CropsInIncrement { get; set; }

        /// <summary>Crops needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public ForagerProgressData()
        {
            TotalCredits = 0;
            CropsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public ForagerProgressData Clone()
        {
            return new ForagerProgressData
            {
                TotalCredits = this.TotalCredits,
                CropsInIncrement = this.CropsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
        public static string GetHeaderString()
        {
            return "FRG";
        }

        public static byte GetVersion() {
            return (byte)2;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(CropsInIncrement);
            writer.Write(CurrentIncrementSize);
            writer.Write(LastActivityDay);
        }

        public static string SAVE_KEY => "sitForagerProgress";
        public static string Description => "foraging";

        public static ForagerProgressData ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new ForagerProgressData
                        {
                            TotalCredits = reader.ReadInt32(),
                            CropsInIncrement = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                case 2:
                    return new ForagerProgressData
                        {
                            TotalCredits = reader.ReadInt32(),
                            CropsInIncrement = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32(),
                            LastActivityDay = reader.ReadDouble()
                        };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingForagerProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, ForagerProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.ForagerProgress;
        }
    }
}