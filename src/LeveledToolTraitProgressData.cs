using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace SeraphLeveling {

    public interface ILevelableToolContract<ToolT>
    {
        public abstract static string Name { get; }
    }
    public abstract class LevelableTool<ToolT>
    {
        public abstract void WriteOut(BinaryWriter writer);
        /// <summary>Points accumulated toward the next credit with this tool.</summary>
        public int PartialCredit { get; set; }

        /// <summary>Points needed for the next credit with this tool (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

    }
    public abstract class LeveledToolTraitProgressData<T, ToolT>: LeveledTraitProgressData<T>
        where T : LeveledToolTraitProgressData<T, ToolT>, IProgressDataContract<T>, ILeveledTraitContract<T>, new()
        where ToolT: LevelableTool<ToolT>, ILevelableToolContract<ToolT>, IDeepCopyable<ToolT>, new()
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

        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(LastActivityDay);

            // Snapshot inner dictionary to avoid concurrent modification
            var toolSnapshot = ToolProgress.ToArray();
            writer.Write(toolSnapshot.Length);
            foreach (var toolKvp in toolSnapshot)
            {
                writer.Write(toolKvp.Key); // Pickaxe code
                toolKvp.Value.WriteOut(writer);
            }
        }

        public override void WriteIncrementLine(StringBuilder sb)
        {
            if (ToolProgress.Count > 0)
            {
                sb.AppendLine($"\nPer-{ToolT.Name} progress:");
                foreach (var kvp in ToolProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string toolName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (toolName.StartsWith("game:"))
                        toolName = toolName.Substring(5);

                    var toolProgress = kvp.Value;
                    sb.AppendLine($"  {toolName}: {toolProgress.PartialCredit}/{toolProgress.CurrentIncrementSize} points");
                }
            }
            else
            {
                sb.AppendLine("\nNo pickaxe progress yet. Mine stone or ore with a pickaxe to start!");
            }

        }
    }
}