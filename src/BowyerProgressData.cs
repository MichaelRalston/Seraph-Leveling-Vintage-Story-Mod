using System;
using System.IO;
using System.Collections.Concurrent;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Bowyer unlock progression.
    /// Tracks bow damage with simple bow and longbow for unlock.
    /// </summary>
    public class BowyerProgressData : ProgressData<BowyerProgressData>, IProgressDataContract<BowyerProgressData>
    {
        /// <summary>Total damage dealt with simple bow or longbow.</summary>
        public float TotalBowDamage { get; set; }

        /// <summary>Whether the Bowyer trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public BowyerProgressData()
        {
            TotalBowDamage = 0;
            IsUnlocked = false;
        }

        public BowyerProgressData Clone()
        {
            return new BowyerProgressData
            {
                TotalBowDamage = this.TotalBowDamage,
                IsUnlocked = this.IsUnlocked
            };
        }

        public static string GetHeaderString()
        {
            return "BWY";
        }

        public static byte GetVersion()
        {
            return (byte)1;
        }

        public static BowyerProgressData ReadVersion(byte version, BinaryReader reader)
        {
            return version switch
            {
                1 => new BowyerProgressData
                {
                    TotalBowDamage = reader.ReadSingle(),
                    IsUnlocked = reader.ReadBoolean()
                },
                _ => throw new NotSupportedException($"Version {version} is not supported"),
            };
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(TotalBowDamage);
            writer.Write(IsUnlocked);
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingBowyerProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, BowyerProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.BowyerProgress;
        }

    }
}