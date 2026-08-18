using Vintagestory.GameContent;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class MaxHealthUnlockedAttributeModifierDefinition : UnlockedStatAttributeModifierDefinition
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public MaxHealthUnlockedAttributeModifierDefinition()
        {
            StatName = "maxhealthExtraPoints";
        }

        protected override void MarkStatDirty(IServerPlayer player)
        {
            player.Entity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }
    }
}
