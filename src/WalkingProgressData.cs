using System.IO;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking walking speed progression.
    /// Simpler than other progression systems since walking has no "tools".
    /// </summary>
    public class WalkingProgressData:ProgressData<WalkingProgressData>,IProgressDataContract
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 15.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Blocks walked toward the next credit.</summary>
        public float BlocksInIncrement { get; set; }

        /// <summary>Blocks needed for the next credit (1000, 2000, 3000, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public WalkingProgressData()
        {
            TotalCredits = 0;
            BlocksInIncrement = 0;
            CurrentIncrementSize = 1000; // Base increment size
            LastActivityDay = 0;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public WalkingProgressData Clone()
        {
            return new WalkingProgressData
            {
                TotalCredits = this.TotalCredits,
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
        public static byte[] GetHeader() {
            return [(byte)0x53, (byte)0x49, (byte)0x57]; // SIW
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

        public static string SAVE_KEY => "sitWalkingProgress";
        public static string Description => "walking";

        public static WalkingProgressData ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new WalkingProgressData {
                        TotalCredits = reader.ReadInt32(),
                        BlocksInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                case 2:
                    return new WalkingProgressData {
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