using System;
using System.IO;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Technical progression.
    /// Binary unlock after repairing translocators.
    /// </summary>
    public class TechnicalProgressData: ProgressData<TechnicalProgressData>, IProgressDataContract<TechnicalProgressData>
    {
        /// <summary>Number of translocators repaired.</summary>
        public int TranslocatorsRepaired { get; set; }

        /// <summary>Whether the Technical trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public TechnicalProgressData()
        {
            TranslocatorsRepaired = 0;
            IsUnlocked = false;
        }

        public TechnicalProgressData Clone()
        {
            return new TechnicalProgressData
            {
                TranslocatorsRepaired = this.TranslocatorsRepaired,
                IsUnlocked = this.IsUnlocked
            };
        }
        public static string GetHeaderString()
        {
            return "TEC";
        }

        public static byte GetVersion() {
            return (byte)1;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TranslocatorsRepaired);
            writer.Write(IsUnlocked);
        }

        public static string SAVE_KEY => "sitTechnicalProgress";
        public static string Description => "technical";

        public static TechnicalProgressData ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new TechnicalProgressData
                    {
                        TranslocatorsRepaired = reader.ReadInt32(),
                        IsUnlocked = reader.ReadBoolean()
                    };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
    }
}