using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using SeraphLeveling.Data.Traits;

namespace SeraphLeveling.Data.Attributes
{
    public record class TechnicalAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<TechnicalAttributeModifierDefinition, TechnicalAttributeModifierProgressData>, IConstructable<TechnicalAttributeModifierDefinition, TechnicalAttributeModifierProgressData>
    {
        public static TechnicalAttributeModifierProgressData Create(TechnicalAttributeModifierDefinition def)
        {
            return new TechnicalAttributeModifierProgressData(def);
        }

        public override void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TotalCredits}/{GlobalMaxCredits} translocators ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }

        public override void ApplyUnlock(IServerPlayer player, TechnicalAttributeModifierProgressData progress)
        {
            base.ApplyUnlock(player, progress);
            
            // Set the temporal gear repair cost reduction stat
            // -1 means one fewer temporal gear needed to repair translocators
            float gearCostReduction = progress.IsUnlocked ? -1f : 0f;
            player.Entity.Stats.Set("temporalGearTLRepairCost", "sitTechnicalBonus", gearCostReduction, false);
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"Translocators repaired: {progress.TotalCredits:F0} / {GlobalMaxCredits:F0}");
            if (!progress.IsUnlocked)
            {
                int remaining = (int)(GlobalMaxCredits - progress.TotalCredits);
                sb.AppendLine($"Repair {remaining} more translocators to unlock!");
            }
        }
    }

    public class TechnicalAttributeModifierProgressData(TechnicalAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<TechnicalAttributeModifierDefinition, TechnicalAttributeModifierProgressData>(definition)
    {
    }
}
