using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class TechnicianAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<TechnicianAttributeModifierDefinition, TechnicianAttributeModifierProgressData>, IConstructable<TechnicianAttributeModifierDefinition, TechnicianAttributeModifierProgressData>
    {
        public static TechnicianAttributeModifierProgressData Create(TechnicianAttributeModifierDefinition def)
        {
            return new TechnicianAttributeModifierProgressData(def);
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"Large gears crafted: {progress.TotalCredits:F0} / {GlobalMaxCredits:F0}");
            if (!progress.IsUnlocked)
            {
                int remaining = (int)(GlobalMaxCredits - progress.TotalCredits);
                sb.AppendLine($"Craft {remaining} more large gears to unlock!");
            }
        }
    }

    public class TechnicianAttributeModifierProgressData(TechnicianAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<TechnicianAttributeModifierDefinition, TechnicianAttributeModifierProgressData>(definition)
    {
    }
}
