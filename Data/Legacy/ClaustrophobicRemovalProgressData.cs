using System;
using System.IO;
using System.Collections.Concurrent;

namespace SeraphLeveling.Data.Legacy
{
    /// <summary>
    /// Data structure for tracking Claustrophobic removal progression (Hunter class).
    /// Removes the Claustrophobic negative trait after reaching mining threshold.
    /// </summary>
    public class ClaustrophobicRemovalProgressData : RemovalProgressData<ClaustrophobicRemovalProgressData>, IProgressDataContract<ClaustrophobicRemovalProgressData>
    {
        public static string GetHeaderString()
        {
            return "CLR";
        }

        public static byte GetVersion()
        {
            return (byte)1;
        }

        public ClaustrophobicRemovalProgressData Clone()
        {
            return new ClaustrophobicRemovalProgressData
            {
                IsRemoved = this.IsRemoved
            };
        }

        public static ClaustrophobicRemovalProgressData ReadVersion(byte version, BinaryReader reader)
        {
            return version switch
            {
                1 => new ClaustrophobicRemovalProgressData
                {
                    IsRemoved = reader.ReadBoolean()
                },
                _ => throw new NotSupportedException($"Version {version} is not supported"),
            };
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingClaustrophobicRemovalProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, ClaustrophobicRemovalProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.ClaustrophobicRemovalProgress;
        }

    }

}
