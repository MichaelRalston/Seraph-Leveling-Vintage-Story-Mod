using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SeraphLeveling
{
    /// <summary>
    /// Data structure for tracking hunger rate progression.
    /// Simpler than other progression systems since hunger has no "tools".
    /// Tracks time spent at full saturation.
    /// </summary>
    public class HungerProgressData : ProgressData<HungerProgressData>, IProgressDataContract<HungerProgressData>
    {
        /// <summary>Total credits earned (each credit = 1% hunger rate reduction). Max 25.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Seconds at full saturation toward the next credit.</summary>
        public float SecondsInIncrement { get; set; }

        /// <summary>Seconds needed for the next credit (300, 360, 420, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public HungerProgressData()
        {
            TotalCredits = 0;
            SecondsInIncrement = 0;
            CurrentIncrementSize = 300; // Base increment size (5 minutes)
            LastActivityDay = 0;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public HungerProgressData Clone()
        {
            return new HungerProgressData
            {
                TotalCredits = this.TotalCredits,
                SecondsInIncrement = this.SecondsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
        public static byte[] GetHeader()
        {
            return [(byte)0x53, (byte)0x49, (byte)0x48]; // SIH
        }

        public static byte GetVersion()
        {
            return (byte)2;
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(TotalCredits);
            writer.Write(SecondsInIncrement);
            writer.Write(CurrentIncrementSize);
            writer.Write(LastActivityDay);
        }

        public static HungerProgressData ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    return new HungerProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        SecondsInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = 0 // Version 1 did not track last activity day
                    };
                case 2:
                    return new HungerProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        SecondsInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };
                default:
                    throw new InvalidOperationException($"Unsupported version {version} for HungerProgressData.");
            }
        }
        public static string SAVE_KEY => "sitHungerProgress";
        public static string Description => "hunger";
    }
}