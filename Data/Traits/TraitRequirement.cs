using System.Text;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Traits
{
    public interface IRequirement {
        public string Name { get; }
        public bool IsSatisfied(IServerPlayer player);

        /// <summary>
        /// Get a status string for the requirement to return when the player uses the /trait command
        /// </summary>
        public abstract void CollectStatus(IPlayer player, StringBuilder sb);

        /// <summary>
        /// Registers a method to be called every time satisfaction of this requirement changes for a player
        /// </summary>
        public event SatisfactionChangedDelegate SatisfactionChanged;
    }

    public delegate void SatisfactionChangedDelegate(IServerPlayer player, bool oldValue, bool newValue);

    public class RequiredUnlockedTrait : IRequirement
    {
        protected TraitDefinition Definition { get; init; }
        public string Name { get => Definition.Id; }

        public RequiredUnlockedTrait(TraitDefinition definition)
        {
            Definition = definition;
            Definition.UnlockChanged += OnTraitUnlockChanged;
        }

        ~RequiredUnlockedTrait()
        {
            Definition.UnlockChanged -= OnTraitUnlockChanged;
        }

        public event SatisfactionChangedDelegate SatisfactionChanged;

        public bool IsSatisfied(IServerPlayer player)
        {
            return (player?.Entity) != null && Definition.HasVanillaTrait(player.Entity);
        }

        public void CollectStatus(IPlayer player, StringBuilder sb)
        {
            sb.AppendLine($"  {Name}: {((player?.Entity) != null && Definition.HasVanillaTrait(player.Entity) ? "UNLOCKED ✓" : "Locked ✗")}");
        }

        protected void OnTraitUnlockChanged(IServerPlayer player, bool oldUnlock, bool newUnlock)
        {
            SatisfactionChanged?.Invoke(player, oldUnlock, newUnlock);
        }
    }

    public class RequiredUnlockedAttribute : IRequirement
    {
        protected IUnlockedAttributeModifierDefinition Definition { get; init; }
        public string Name { get => Definition.Name; }

        public RequiredUnlockedAttribute(IUnlockedAttributeModifierDefinition definition)
        {
            Definition = definition;
            Definition.UnlockChanged += OnAttributeUnlockChanged;
        }

        ~RequiredUnlockedAttribute()
        {
            Definition.UnlockChanged -= OnAttributeUnlockChanged;
        }

        public event SatisfactionChangedDelegate SatisfactionChanged;

        public bool IsSatisfied(IServerPlayer player)
        {
            return Definition.IsUnlockedForPlayer(player);
        }

        public void CollectStatus(IPlayer player, StringBuilder sb)
        {
            sb.AppendLine($"  {Name}: {(Definition.IsUnlockedForPlayer(player) ? "UNLOCKED ✓" : "Locked ✗")}");
        }

        protected void OnAttributeUnlockChanged(IServerPlayer player, bool oldUnlock, bool newUnlock)
        {
            SatisfactionChanged?.Invoke(player, oldUnlock, newUnlock);
        }
    }

    public class RequiredLeveledAttribute : IRequirement
    {
        protected ILeveledAttributeModifierDefinition Definition { get; init; }
        protected int RequiredBonusPercent { get; init; }
        public string Name { get => Definition.Name; }

        public RequiredLeveledAttribute(ILeveledAttributeModifierDefinition definition, int requiredBonusPercent)
        {
            Definition = definition;
            RequiredBonusPercent = requiredBonusPercent;
            Definition.BonusChanged += OnAttributeBonusChanged;
        }

        ~RequiredLeveledAttribute()
        {
            Definition.BonusChanged -= OnAttributeBonusChanged;
        }

        public event SatisfactionChangedDelegate SatisfactionChanged;

        public bool IsSatisfied(IServerPlayer player)
        {
            return (player?.Entity != null) && Definition.GetBonusPercent(player.Entity) >= RequiredBonusPercent;
        }

        public void CollectStatus(IPlayer player, StringBuilder sb)
        {
            if (player?.Entity != null)
            {
                sb.AppendLine($"  {Name} level: {Definition.GetBonusPercent(player.Entity)}% / {RequiredBonusPercent}% ({(Definition.GetBonusPercent(player.Entity) >= RequiredBonusPercent ? "✓" : "✗")})");
            }
        }

        protected void OnAttributeBonusChanged(IServerPlayer player, int oldBonusPercent, int newBonusPercent)
        {
            bool oldSatisfaction = oldBonusPercent >= RequiredBonusPercent;
            bool newSatisfaction = newBonusPercent >= RequiredBonusPercent;
            if (oldSatisfaction != newSatisfaction)
            {
                SatisfactionChanged?.Invoke(player, oldSatisfaction, newSatisfaction);
            }
        }
    }
}
