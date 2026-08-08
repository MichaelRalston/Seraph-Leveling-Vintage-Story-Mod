using System;
using System.IO;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Furtive progression.
    /// Tracks blocks of sneaking for animal detection range reduction.
    /// </summary>
    public class FurtiveProgressData: ProgressData<FurtiveProgressData>, IProgressDataContract<FurtiveProgressData>
    {
        /// <summary>Total credits earned (each credit = -1% animal detection range). Max 35.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Sneaking blocks accumulated toward the next credit.</summary>
        public float BlocksInIncrement { get; set; }

        /// <summary>Blocks needed for the next credit (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public FurtiveProgressData()
        {
            TotalCredits = 0;
            BlocksInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
            LastActivityDay = 0;
        }

        public FurtiveProgressData Clone()
        {
            return new FurtiveProgressData
            {
                TotalCredits = this.TotalCredits,
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }

        public static string GetHeaderString()
        {
            return "FUR";
        }

        public static byte GetVersion() {
            return (byte)2;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(BlocksInIncrement);
            writer.Write(CurrentIncrementSize);
            writer.Write(LastActivityDay);
        }

        public static string SAVE_KEY => "sitFurtiveProgress";
        public static string Description => "furtive";

        public static FurtiveProgressData ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new FurtiveProgressData {
                        TotalCredits = reader.ReadInt32(),
                        BlocksInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                case 2:
                    return new FurtiveProgressData {
                        TotalCredits = reader.ReadInt32(),
                        BlocksInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
    }
}