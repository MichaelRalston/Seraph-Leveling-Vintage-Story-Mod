using System.IO;
using System;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Improviser unlock progression.
    /// Tracks damage dealt with thrown rocks for sling unlock.
    /// </summary>
    public class ImproviserProgressData: ProgressData<ImproviserProgressData>, IProgressDataContract<ImproviserProgressData>
    {
        /// <summary>Total damage dealt with thrown rocks.</summary>
        public float TotalRockDamage { get; set; }

        /// <summary>Whether the Improviser trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public ImproviserProgressData()
        {
            TotalRockDamage = 0;
            IsUnlocked = false;
        }

        public ImproviserProgressData Clone()
        {
            return new ImproviserProgressData
            {
                TotalRockDamage = this.TotalRockDamage,
                IsUnlocked = this.IsUnlocked
            };
        }

        public static string GetHeaderString()
        {
            return "IMP";
        }

        public static byte GetVersion() {
            return (byte)2;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalRockDamage);
            writer.Write(IsUnlocked);
        }

        public static string SAVE_KEY => "sitImproviserProgress";
        public static string Description => "improviser";

        public static ImproviserProgressData ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new ImproviserProgressData
                    {
                        TotalRockDamage = reader.ReadSingle(),
                        IsUnlocked = reader.ReadBoolean()
                    };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
    }
}