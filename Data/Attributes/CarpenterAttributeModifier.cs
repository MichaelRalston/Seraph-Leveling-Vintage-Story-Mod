using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class CarpenterAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<CarpenterAttributeModifierDefinition, CarpenterAttributeModifierProgressData>, IConstructable<CarpenterAttributeModifierDefinition, CarpenterAttributeModifierProgressData>
    {
        public static CarpenterAttributeModifierProgressData Create(CarpenterAttributeModifierDefinition def)
        {
            return new CarpenterAttributeModifierProgressData(def);
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"Boards crafted: {progress.TotalCredits:F0} / {GlobalMaxCredits:F0}");
            if (!progress.IsUnlocked)
            {
                int remaining = (int)(GlobalMaxCredits - progress.TotalCredits);
                sb.AppendLine($"Craft {remaining} more boards to unlock!");
            }
        }
    }

    public class CarpenterAttributeModifierProgressData(CarpenterAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<CarpenterAttributeModifierDefinition, CarpenterAttributeModifierProgressData>(definition)
    {
    }
}
