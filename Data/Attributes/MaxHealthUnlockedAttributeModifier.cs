using Vintagestory.GameContent;
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
            SeraphLevelingModSystem.ServerApi.Logger.Debug($"[Verdus] Calling ApplyUnlock for max health attribute {Id}, unlocked={progress.IsUnlocked}");
            if (player?.Entity == null) return;

            base.ApplyUnlock(player, progress);

            if (progress.IsUnlocked)
            {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[Verdus] Setting max health stat for attribute {Id}, category={STAT_CATEGORY}, statkey={StatKey}, modifier={ModifierAmount}");
                player.Entity.Stats.Set(STAT_CATEGORY, StatKey, ModifierAmount, false);
            }
            else
            {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[Verdus] Removing max health stat for attribute {Id}, category={STAT_CATEGORY}, statkey={StatKey}");
                player.Entity.Stats.Remove(STAT_CATEGORY, StatKey);
            }
            
            // Calling this forces the behavior to recalculate MaxHealth using the new stats
            player.Entity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }
    }

    public class MaxHealthUnlockedAttributeModifierProgressData(MaxHealthUnlockedAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<MaxHealthUnlockedAttributeModifierDefinition, MaxHealthUnlockedAttributeModifierProgressData>(definition)
    {
        
    }
}
