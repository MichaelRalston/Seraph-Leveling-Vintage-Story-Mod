using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class PropagatorAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<PropagatorAttributeModifierDefinition, PropagatorAttributeModifierProgressData>, IConstructable<PropagatorAttributeModifierDefinition, PropagatorAttributeModifierProgressData>
    {
        public static PropagatorAttributeModifierProgressData Create(PropagatorAttributeModifierDefinition def)
        {
            return new PropagatorAttributeModifierProgressData(def);
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"Compost crafted: {progress.TotalCredits:F0} / {GlobalMaxCredits:F0}");
            if (!progress.IsUnlocked)
            {
                int remaining = (int)(GlobalMaxCredits - progress.TotalCredits);
                sb.AppendLine($"Craft {remaining} more compost to unlock!");
            }
        }
    }

    public class PropagatorAttributeModifierProgressData(PropagatorAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<PropagatorAttributeModifierDefinition, PropagatorAttributeModifierProgressData>(definition)
    {
    }
}
