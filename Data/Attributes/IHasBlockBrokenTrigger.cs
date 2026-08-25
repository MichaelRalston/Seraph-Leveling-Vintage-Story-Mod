using System;
using System.Collections.Concurrent;
using SeraphLeveling.Data.Tools;
using SeraphLeveling.Util;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public delegate void TriggerBlockBrokenDelegate(IServerPlayer player, AssetLocation toolCode, int blockId, BlockPos blockPos);

    public interface IHasBlockBrokenTrigger<D, PD, E> : ISaveableAttribute where PD : LeveledToolAttributeModifierProgressData<D, PD, E> where D : LeveledToolAttributeModifierDefinition<D, PD, E>, IConstructable<D, PD> where E : Enum
    {
        public ToolDefinition Tool { get; }
        public ConcurrentDictionary<IAssetLocationMatcher, float> BrokenBlockScores { get; }

        public PD GetForPlayer(string playerUid);

        protected virtual float GetBlockScore(int blockId, BlockPos blockPos)
        {
            var blockCode = SeraphLevelingModSystem.GetBlockCode(blockId);
            if (blockCode == null || !blockCode.Valid || BrokenBlockScores == null)
            {
                return 0;
            }
            foreach (var matcher in BrokenBlockScores.Keys)
            {
                if (matcher.Matches(blockCode))
                {
                    return BrokenBlockScores[matcher];
                }
            }
            return 0;
        }

        public void OnTriggerBlockBroken(IServerPlayer player, AssetLocation toolCode, int blockId, BlockPos blockPos)
        {
            if (player == null || !Tool.Matches(toolCode) || !SeraphLevelingModSystem.LoadedAttributes.Contains(this))
            {
                return;
            }
            else
            {
                var score = GetBlockScore(blockId, blockPos);
                if (score > 0)
                {
                    GetForPlayer(player.PlayerUID).DoEvent(player, toolCode, score);
                }
            }
        }
    }
}
