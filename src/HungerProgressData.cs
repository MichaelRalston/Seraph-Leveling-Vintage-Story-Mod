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
    public class HungerProgressData:ProgressData
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
    }
}