using System;
using System.IO;
using System.Collections.Concurrent;

namespace SeraphLeveling.Data.Legacy
{
    /// <summary>
    /// Data structure for tracking Merciless unlock progression.
    /// Unlocks after reaching armor durability and melee damage thresholds.
    /// </summary>
    public class MercilessProgressData : ProgressData<MercilessProgressData>, IProgressDataContract<MercilessProgressData>
    {
        /// <summary>Whether the Merciless trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }
        public static string SAVE_KEY { get { return "sitMercilessProgress"; } }

        public MercilessProgressData()
        {
            IsUnlocked = false;
        }

        public MercilessProgressData Clone()
        {
            return new MercilessProgressData
            {
                IsUnlocked = this.IsUnlocked
            };
        }

        public static string GetHeaderString()
        {
            return "MRC";
        }

        public static byte GetVersion() {
            return (byte)1;
        }

        public MercilessProgressData(BinaryReader reader)
        {
            IsUnlocked = reader.ReadBoolean();
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(this.IsUnlocked);
        }

        public static MercilessProgressData ReadVersion(byte version, BinaryReader reader)
        {
            return version switch
            {
                1 => new MercilessProgressData
                {
                    IsUnlocked = reader.ReadBoolean()
                },
                _ => throw new NotSupportedException($"Version {version} is not supported"),
            };
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingMercilessProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, MercilessProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.MercilessProgress;
        }
    }
}
