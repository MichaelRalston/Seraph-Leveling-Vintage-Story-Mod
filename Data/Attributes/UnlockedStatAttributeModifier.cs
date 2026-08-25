using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class UnlockedStatAttributeModifierDefinition : UnlockedAttributeModifierDefinition<UnlockedStatAttributeModifierDefinition, UnlockedStatAttributeModifierProgressData>, IConstructable<UnlockedStatAttributeModifierDefinition, UnlockedStatAttributeModifierProgressData>
    {
        public required virtual string StatName { get; init; }
        public required float ModifierAmount { get; set; }
        public virtual bool ModifierIsPercentage { get; init; } = false;
        public virtual string StatKey { get => field ??= $"sit{FlatName}Bonus"; init; }

        public static UnlockedStatAttributeModifierProgressData Create(UnlockedStatAttributeModifierDefinition def)
        {
            return new UnlockedStatAttributeModifierProgressData(def);
        }

        public override void ApplyUnlock(IServerPlayer player, UnlockedStatAttributeModifierProgressData progress)
        {
            if (player?.Entity == null) return;

            base.ApplyUnlock(player, progress);

            if (progress.IsUnlocked)
            {
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

    public class UnlockedStatAttributeModifierProgressData(UnlockedStatAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<UnlockedStatAttributeModifierDefinition, UnlockedStatAttributeModifierProgressData>(definition)
    {
        
    }
}
