using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class MasonAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<MasonAttributeModifierDefinition, MasonAttributeModifierProgressData>, IConstructable<MasonAttributeModifierDefinition, MasonAttributeModifierProgressData>
    {
        public static MasonAttributeModifierProgressData Create(MasonAttributeModifierDefinition def)
        {
            return new MasonAttributeModifierProgressData(def);
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"Ashlar blocks crafted: {progress.TotalCredits:F0} / {GlobalMaxCredits:F0}");
            if (!progress.IsUnlocked)
            {
                int remaining = (int)(GlobalMaxCredits - progress.TotalCredits);
                sb.AppendLine($"Craft {remaining} more ashlar blocks to unlock!");
            }
        }
    }

    public class MasonAttributeModifierProgressData(MasonAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<MasonAttributeModifierDefinition, MasonAttributeModifierProgressData>(definition)
    {
    }
}
