using System;
using System.IO;

namespace SeraphLeveling 
{
    /// <summary>
    /// Data structure for tracking Heavy Footed removal progression (Lumberjack class).
    /// Removes the Heavy Footed negative trait after reaching appropriate thresholds
    /// </summary>
    public class HeavyFootedRemovalProgressData : RemovalProgressData<HeavyFootedRemovalProgressData>, IProgressDataContract<HeavyFootedRemovalProgressData>
    {
        public static byte[] GetHeader()
        {
            return [(byte)0x48, (byte)0x56, (byte)0x46]; // HVF
        }

        public static byte GetVersion()
        {
            return (byte)1;
        }

        public HeavyFootedRemovalProgressData Clone()
        {
            return new HeavyFootedRemovalProgressData
            {
                IsRemoved = this.IsRemoved
            };
        }

        public static HeavyFootedRemovalProgressData ReadVersion(byte version, BinaryReader reader)
        {
            return version switch
            {
                1 => new HeavyFootedRemovalProgressData
                {
                    IsRemoved = reader.ReadBoolean()
                },
                _ => throw new NotSupportedException($"Version {version} is not supported"),
            };
        }
    }

}