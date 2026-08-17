using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class MaxHealthUnlockedAttributeModifierDefinition : UnlockedAttributeModifierDefinition<MaxHealthUnlockedAttributeModifierDefinition, MaxHealthUnlockedAttributeModifierProgressData>, IConstructable<MaxHealthUnlockedAttributeModifierDefinition, MaxHealthUnlockedAttributeModifierProgressData>
    {
        protected const string STAT_CATEGORY = "maxhealthExtraPoints";

        public required float ModifierAmount { get; set; }
        public virtual string StatKey { get => field ??= $"sit{Name}Bonus"; init; }

        public static MaxHealthUnlockedAttributeModifierProgressData Create(MaxHealthUnlockedAttributeModifierDefinition def)
        {
            return new MaxHealthUnlockedAttributeModifierProgressData(def);
        }

        public override void ApplyUnlock(IServerPlayer player, MaxHealthUnlockedAttributeModifierProgressData progress)
        {
            if (player?.Entity == null) return;

            base.ApplyUnlock(player, progress);

            if (progress.IsUnlocked)
            {
                player.Entity.Stats.Set(STAT_CATEGORY, StatKey, ModifierAmount, false);
            }
            else
            {
                player.Entity.Stats.Remove(STAT_CATEGORY, StatKey);
            }
        }
    }

    public class MaxHealthUnlockedAttributeModifierProgressData(MaxHealthUnlockedAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<MaxHealthUnlockedAttributeModifierDefinition, MaxHealthUnlockedAttributeModifierProgressData>(definition)
    {
        
    }
}
