using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Util;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking Clothier progression.
    /// Tracks unique clothing items worn to unlock sewing kit crafting.
    /// </summary>
    public class ClothierProgressData : ProgressData<ClothierProgressData>, IProgressDataContract<ClothierProgressData>
    {
        /// <summary>Set of unique clothing item codes that have been worn.</summary>
        public HashSet<string> UniqueClothesWorn { get; set; }

        /// <summary>Whether the sewing kit crafting has been unlocked.</summary>
        public bool SewingKitUnlocked { get; set; }

        public ClothierProgressData()
        {
            UniqueClothesWorn = [];
            SewingKitUnlocked = false;
        }

        public ClothierProgressData Clone()
        {
            return new ClothierProgressData
            {
                UniqueClothesWorn = [.. this.UniqueClothesWorn],
                SewingKitUnlocked = this.SewingKitUnlocked
            };
        }

        public static string GetHeaderString()
        {
            return "CLT";
        }

        public static byte GetVersion()
        {
            return (byte)1;
        }

        public static ClothierProgressData ReadVersion(byte version, BinaryReader reader)
        {
            return version switch
            {
                1 => new ClothierProgressData
                {
                    SewingKitUnlocked = reader.ReadBoolean(),
                    UniqueClothesWorn = [.. reader.ReadStringArray()]
                },
                _ => throw new NotSupportedException($"Version {version} is not supported"),
            };
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(SewingKitUnlocked);
            writer.WriteArray(UniqueClothesWorn.ToArray());
        }
    }
}