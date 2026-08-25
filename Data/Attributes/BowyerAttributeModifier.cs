using System.Collections.Generic;
using System.Text;
using SeraphLeveling.Data.Tools;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class BowyerAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<BowyerAttributeModifierDefinition, BowyerAttributeModifierProgressData>, 
        IConstructable<BowyerAttributeModifierDefinition, BowyerAttributeModifierProgressData>,
        IHasDamageScoredTrigger<BowyerAttributeModifierDefinition, BowyerAttributeModifierProgressData>
    {
        public BowyerAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.DamageDealtTrigger += ((IHasDamageScoredTrigger<BowyerAttributeModifierDefinition, BowyerAttributeModifierProgressData>)this).OnTriggerDamageDealt;
        }

        ~BowyerAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.DamageDealtTrigger -= ((IHasDamageScoredTrigger<BowyerAttributeModifierDefinition, BowyerAttributeModifierProgressData>)this).OnTriggerDamageDealt;
        }

        public required List<ToolDefinition> Weapons { get; init; }

        public static BowyerAttributeModifierProgressData Create(BowyerAttributeModifierDefinition def)
        {
            return new BowyerAttributeModifierProgressData(def);
        }

        public override void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TotalCredits:F0}/{GlobalMaxCredits:F0} {CreditDescription} ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"Bow damage: {progress.TotalCredits:F0} / {GlobalMaxCredits:F0} ({(progress.TotalCredits >= GlobalMaxCredits ? "UNLOCKED" : "locked")})");
        }
    }

    public class BowyerAttributeModifierProgressData(BowyerAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<BowyerAttributeModifierDefinition, BowyerAttributeModifierProgressData>(definition)
    {
    }
}
