using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class ImproviserAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<ImproviserAttributeModifierDefinition, ImproviserAttributeModifierProgressData>, IConstructable<ImproviserAttributeModifierDefinition, ImproviserAttributeModifierProgressData>
    {
        public static ImproviserAttributeModifierProgressData Create(ImproviserAttributeModifierDefinition def)
        {
            return new ImproviserAttributeModifierProgressData(def);
        }

        public override void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TotalCredits:F0}/{GlobalMaxCredits:F0} {CreditDescription} ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"Thrown rock damage: {progress.TotalCredits:F0} / {GlobalMaxCredits:F0} ({(progress.TotalCredits >= GlobalMaxCredits ? "UNLOCKED" : "locked")})");
        }
    }

    public class ImproviserAttributeModifierProgressData(ImproviserAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<ImproviserAttributeModifierDefinition, ImproviserAttributeModifierProgressData>(definition)
    {
    }
}
