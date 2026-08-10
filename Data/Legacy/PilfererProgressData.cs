using System.Collections.Concurrent;
using System;
using System.IO;

namespace SeraphLeveling.Data.Legacy
{
    /// <summary>
    /// Data structure for tracking Pilferer progression.
    /// Tracks loot vessels broken for loot bonuses.
    /// </summary>
    public class PilfererProgressData: ProgressData<PilfererProgressData>, IProgressDataContract<PilfererProgressData>
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Points accumulated toward the next credit.</summary>
        public int PointsInIncrement { get; set; }

        /// <summary>Points needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public PilfererProgressData()
        {
            TotalCredits = 0;
            PointsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public PilfererProgressData Clone()
        {
            return new PilfererProgressData
            {
                TotalCredits = this.TotalCredits,
                PointsInIncrement = this.PointsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
         public static string GetHeaderString()
        {
            return "PLF";
        }

        public static byte GetVersion() {
            return (byte)3;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(PointsInIncrement);
            writer.Write(CurrentIncrementSize);
            writer.Write(LastActivityDay);
        }

        public static string SAVE_KEY => "sitPilfererProgress";
        public static string Description => "pilferer";

        public static PilfererProgressData ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    var progress = new PilfererProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        PointsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                    // Version 1 had chest positions - skip them
                    int chestCount = reader.ReadInt32();
                    for (int j = 0; j < chestCount; j++)
                    {
                        reader.ReadString(); // Skip old chest position data
                    }
                    return progress;
                case 2:
                    return new PilfererProgressData
                        {
                            TotalCredits = reader.ReadInt32(),
                            PointsInIncrement = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                case 3:
                    return new PilfererProgressData
                        {
                            TotalCredits = reader.ReadInt32(),
                            PointsInIncrement = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32(),
                            LastActivityDay = reader.ReadDouble()
                        };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingPilfererProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, PilfererProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.PilfererProgress;
        }
   }
}
