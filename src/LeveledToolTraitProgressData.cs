using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace SeraphLeveling {
    public abstract class LeveledToolTraitProgressData<T, ToolT>: LeveledTraitProgressData<T>
        where T : LeveledToolTraitProgressData<T, ToolT>, IProgressDataContract<T>, ILeveledTraitContract<T>, new()
        where ToolT: IDeepCopyable<ToolT>, new()
    {
        /// <summary>Per-tool progress tracking. Key is item code (e.g., "game:pickaxe-copper").</summary>
        public Dictionary<string, ToolT> ToolProgress { get; set; }
        public LeveledToolTraitProgressData()
        {
            TotalCredits = 0;
            ToolProgress = new Dictionary<string, ToolT>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific tool.
        /// New tools start with the configured BaseBlocksPerIncrement.
        /// </summary>
        public ToolT GetToolProgress(string toolCode)
        {
            if (!ToolProgress.TryGetValue(toolCode, out var progress))
            {
                progress = new ToolT();
                ToolProgress[toolCode] = progress;
            }
            return progress;
        }
        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public T Clone()
        {
            var clone = new T
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                ToolProgress = new Dictionary<string, ToolT>()
            };
            foreach (var kvp in this.ToolProgress)
            {
                clone.ToolProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }

    }
}