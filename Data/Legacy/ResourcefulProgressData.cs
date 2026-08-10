using System.Collections.Concurrent;
using System;
using System.IO;

namespace SeraphLeveling.Data.Legacy
{
    /// <summary>
    /// Data structure for tracking Resourceful progression.
    /// Tracks animal harvesting for loot and speed bonuses.
    /// </summary>
    public class ResourcefulProgressData: ProgressData<ResourcefulProgressData>, IProgressDataContract<ResourcefulProgressData>
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Animals harvested toward the next credit.</summary>
        public int AnimalsInIncrement { get; set; }

        /// <summary>Animals needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public ResourcefulProgressData()
        {
            TotalCredits = 0;
            AnimalsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public ResourcefulProgressData Clone()
        {
            return new ResourcefulProgressData
            {
                TotalCredits = this.TotalCredits,
                AnimalsInIncrement = this.AnimalsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
        public static string GetHeaderString()
        {
            return "RSF";
        }

        public static byte GetVersion() {
            return (byte)2;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(AnimalsInIncrement);
            writer.Write(CurrentIncrementSize);
            writer.Write(LastActivityDay);
        }

        public static string SAVE_KEY => "sitResourcefulProgress";
        public static string Description => "resourceful";

        public static ResourcefulProgressData ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new ResourcefulProgressData {
                        TotalCredits = reader.ReadInt32(),
                        AnimalsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                case 2:
                    return new ResourcefulProgressData {
                        TotalCredits = reader.ReadInt32(),
                        AnimalsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingResourcefulProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, ResourcefulProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.ResourcefulProgress;
        }
    }
}
