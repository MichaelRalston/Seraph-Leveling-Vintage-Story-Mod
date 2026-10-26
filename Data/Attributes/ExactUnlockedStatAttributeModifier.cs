using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class ExactUnlockedStatAttributeModifierDefinition : UnlockedAttributeModifierDefinition<ExactUnlockedStatAttributeModifierDefinition, ExactUnlockedStatAttributeModifierProgressData>, IConstructable<ExactUnlockedStatAttributeModifierDefinition, ExactUnlockedStatAttributeModifierProgressData>
    {
        public required virtual string StatName { get; init; }
        public required float ModifierAmount { get; set; }
        public virtual bool ModifierIsPercentage { get; init; } = false;
        public virtual string StatKey { get => field ??= $"slrc{StatName}Bonus"; init; }
        public static ExactUnlockedStatAttributeModifierProgressData Create(ExactUnlockedStatAttributeModifierDefinition def)
        {
            return new ExactUnlockedStatAttributeModifierProgressData(def);
        }
        public override void ApplyUnlock(IServerPlayer player, ExactUnlockedStatAttributeModifierProgressData progress)
        {
            if (player?.Entity == null) return;

            base.ApplyUnlock(player, progress);

            if (progress.IsUnlocked)
            {
                var existingStat = player.Entity.Stats[StatName];
                if (existingStat != null && existingStat.ValuesByKey.TryGetValue(StatKey, out var stat))
                {
                    if (stat.Value >= ModifierAmount)
                    {
                        return; // Don't overwrite a bigger value.
                    }
                }
                player.Entity.Stats.Set(StatName, StatKey, ModifierAmount, false);
            }
            else
            {
                player.Entity.Stats.Remove(StatName, StatKey);
            }

            MarkStatDirty(player);
        }

        protected virtual void MarkStatDirty(IServerPlayer player)
        {
            // Do nothing by default, most stats don't need to be explicitly marked dirty
        }

    }
    public class ExactUnlockedStatAttributeModifierProgressData(ExactUnlockedStatAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<ExactUnlockedStatAttributeModifierDefinition, ExactUnlockedStatAttributeModifierProgressData>(definition)
    {

    }
}
