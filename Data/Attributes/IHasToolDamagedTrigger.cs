using System.Collections.Generic;
using SeraphLeveling.Data.Tools;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public delegate void TriggerToolDamagedDelegate(IServerPlayer player, AssetLocation toolCode, float score);

    public interface IHasToolDamagedTrigger<D, PD> : ISaveableAttribute where PD : LeveledToolAttributeModifierProgressData<D, PD, RepairableToolProgress> where D : LeveledToolAttributeModifierDefinition<D, PD, RepairableToolProgress>, IConstructable<D, PD>
    {
        public ToolDefinition Tool { get; }

        public PD GetForPlayer(string playerUid);

        public void OnTriggerToolDamaged(IServerPlayer player, AssetLocation toolCode, float score)
        {
            if (player == null || score <= 0 || !Tool.Matches(toolCode) || !SeraphLevelingModSystem.LoadedAttributes.Contains(this))
            {
                return;
            }
            else
            {
                GetForPlayer(player.PlayerUID).DoEvent(player, toolCode, score, RepairableToolProgress.Usage);
            }
        }
    }    
}
