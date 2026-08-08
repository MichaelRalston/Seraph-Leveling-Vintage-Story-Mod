using System.IO;
using System;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Mender progression.
    /// Tracks repairs done with sewing kit to earn armor/clothing durability bonuses.
    /// </summary>
    public class MenderProgressData: ProgressData<MenderProgressData>, IProgressDataContract<MenderProgressData>
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Repairs done toward the next credit.</summary>
        public int RepairsInIncrement { get; set; }

        /// <summary>Repairs needed for the next credit (5, 6, 7, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public MenderProgressData()
        {
            TotalCredits = 0;
            RepairsInIncrement = 0;
            CurrentIncrementSize = 5; // Base increment size
            LastActivityDay = 0;
        }

        public MenderProgressData Clone()
        {
            return new MenderProgressData
            {
                TotalCredits = this.TotalCredits,
                RepairsInIncrement = this.RepairsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }

        public static string GetHeaderString()
        {
            return "MND";
        }

        public static byte GetVersion() {
            return (byte)2;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(RepairsInIncrement);
            writer.Write(CurrentIncrementSize);
            writer.Write(LastActivityDay);
        }

        public static string SAVE_KEY => "sitMenderProgress";
        public static string Description => "mending";

        public static MenderProgressData ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new MenderProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        RepairsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                case 2:
                    return new MenderProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        RepairsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

    }
}