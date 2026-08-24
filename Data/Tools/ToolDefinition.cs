using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Tools
{
    public record class ToolDefinition
    {
        public string Name { get; init; }
        public HashSet<EnumTool> ValidTools { get; init; }

        protected string MatchPrefix { get => field ??= Name + "-"; init; }

        public ToolDefinition(string name)
        {
            Name = name;
            ValidTools = [];
        }

        public ToolDefinition(string name, EnumTool tool) : this(name)
        {
            ValidTools = [tool];
        }

        public ToolDefinition(string name, HashSet<EnumTool> tools) : this(name)
        {
            ValidTools = tools;
        }

        public bool Matches(ItemStack itemStack)
        {
            var stackToolType = itemStack?.Item?.Tool;
            return stackToolType.HasValue && ValidTools.Any(t => t == stackToolType.Value);
        }

        public bool Matches(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode))
            {
                return false;
            }
            else
            {
                return itemCode.StartsWith(MatchPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
