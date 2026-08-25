using System;
using System.Collections.Generic;
using System.Linq;
using SeraphLeveling.Util;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Tools
{
    public record class ToolDefinition
    {
        public string Name { get; init; }
        public HashSet<EnumTool> ValidTools { get; init; }
        public IAssetLocationMatcher MatchOverride { get; init; } = null;

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

        public bool Matches(AssetLocation itemCode)
        {
            if (itemCode.Path.Contains('+'))
            {
                // If the given item code is a combination of items, e.g. "sling+stone", then split it up and match if any of the parts are a match
                return itemCode.Path.Split('+').Select(token => AssetLocation.Create(token, itemCode.Domain)).Any(MatchesInner);
            }
            else
            {
                return MatchesInner(itemCode);
            }
        }

        protected bool MatchesInner(AssetLocation itemCode)
        {
            if (MatchOverride != null)
            {
                return MatchOverride.Matches(itemCode);
            }
            else if (string.IsNullOrEmpty(itemCode))
            {
                return false;
            }
            else
            {
                return itemCode.Path.StartsWith(MatchPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
