using Vintagestory.GameContent;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class UnlockedStatAttributeModifierDefinition : UnlockedAttributeModifierDefinition<UnlockedStatAttributeModifierDefinition, UnlockedStatAttributeModifierProgressData>, IConstructable<UnlockedStatAttributeModifierDefinition, UnlockedStatAttributeModifierProgressData>
    {
        public required virtual string StatName { get; init; }
        public required float ModifierAmount { get; set; }
        public virtual string StatKey { get => field ??= $"sit{Name}Bonus"; init; }

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

            player.Entity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }
    }

    public class UnlockedStatAttributeModifierProgressData(UnlockedStatAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<UnlockedStatAttributeModifierDefinition, UnlockedStatAttributeModifierProgressData>(definition)
    {
        
    }
}
