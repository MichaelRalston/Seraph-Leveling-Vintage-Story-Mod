using System;
using System.IO;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Claustrophobic removal progression (Hunter class).
    /// Removes the Claustrophobic negative trait after reaching mining threshold.
    /// </summary>
    public class ClaustrophobicRemovalProgressData : RemovalProgressData<ClaustrophobicRemovalProgressData>, IProgressDataContract<ClaustrophobicRemovalProgressData>
    {
        public static byte[] GetHeader()
        {
            return [(byte)0x43, (byte)0x4C, (byte)0x52]; // CLR
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
    }

}