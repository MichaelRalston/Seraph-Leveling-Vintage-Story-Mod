using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class AlchemistAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<AlchemistAttributeModifierDefinition, AlchemistAttributeModifierProgressData>, IConstructable<AlchemistAttributeModifierDefinition, AlchemistAttributeModifierProgressData>
    {
        public static AlchemistAttributeModifierProgressData Create(AlchemistAttributeModifierDefinition def)
        {
            return new AlchemistAttributeModifierProgressData(def);
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"Poultices crafted: {progress.TotalCredits:F0} / {GlobalMaxCredits:F0}");
            if (!progress.IsUnlocked)
            {
                int remaining = (int)(GlobalMaxCredits - progress.TotalCredits);
                sb.AppendLine($"Craft {remaining} more poultices to unlock!");
            }
        }
    }

    public class AlchemistAttributeModifierProgressData(AlchemistAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<AlchemistAttributeModifierDefinition, AlchemistAttributeModifierProgressData>(definition)
    {
    }
}
