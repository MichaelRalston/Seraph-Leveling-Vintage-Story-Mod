using System.Collections.Generic;
using SeraphLeveling.Data.Tools;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public delegate void TriggerToolRepairDelegate(IServerPlayer player, string toolCode, float score);

    public interface IHasToolRepairTrigger<D, PD> where PD : LeveledToolAttributeModifierProgressData<D, PD, RepairableToolProgress> where D : LeveledToolAttributeModifierDefinition<D, PD, RepairableToolProgress>, IConstructable<D, PD>
    {
        public ToolDefinition Tool { get; }
        public List<TriggerToolRepairDelegate> ToolRepairListeners { get; init; }

        public PD GetForPlayer(string playerUid);

        protected void OnTriggerToolRepair(IServerPlayer player, string toolCode, float score)
        {
            if (player == null || score <= 0 || !Tool.Matches(toolCode))
            {
                return;
            }
            else
            {
                GetForPlayer(player.PlayerUID).DoEvent(player, toolCode, score, RepairableToolProgress.Repair);
            }
        }
    }
}
