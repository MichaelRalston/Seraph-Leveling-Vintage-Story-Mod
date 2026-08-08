using System.IO;

namespace SeraphLeveling
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

        public static byte[] GetHeader() {
            return [(byte)0x4D, (byte)0x52, (byte)0x43]; // MRC
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
    }
}