using System.Text;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Traits
{
    public interface IRequiredAttribute {
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

    public class RequiredUnlockedAttribute : IRequiredAttribute
    {
        protected IUnlockedAttributeModifierDefinition Definition { get; init; }
        public string Name { get => Definition.Name; }

        public RequiredUnlockedAttribute(IUnlockedAttributeModifierDefinition definition)
        {
            Definition = definition;
            Definition.UnlockChanged += OnAttributeUnlockChanged;
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

    public class RequiredLeveledAttribute : IRequiredAttribute
    {
        protected ILeveledAttributeModifierDefinition Definition { get; init; }
        protected int RequiredCredits { get; init; }
        public string Name { get => Definition.Name; }

        public RequiredLeveledAttribute(ILeveledAttributeModifierDefinition definition, int requiredCredits)
        {
            Definition = definition;
            RequiredCredits = requiredCredits;
            Definition.CreditsChanged += OnAttributeCreditsChanged;
        }

        public event SatisfactionChangedDelegate SatisfactionChanged;

        public bool IsSatisfied(IServerPlayer player)
        {
            return Definition.IsLeveledForPlayer(player, RequiredCredits);
        }

        public void CollectStatus(IPlayer player, StringBuilder sb)
        {
            sb.AppendLine($"  {Name} level: {Definition.GetCreditsForPlayer(player)} / {RequiredCredits} ({(Definition.GetCreditsForPlayer(player) >= RequiredCredits ? "✓" : "✗")})");
        }

        protected void OnAttributeCreditsChanged(IServerPlayer player, int oldCredits, int newCredits)
        {
            bool oldSatisfaction = oldCredits >= RequiredCredits;
            bool newSatisfaction = newCredits >= RequiredCredits;
            if (oldSatisfaction != newSatisfaction)
            {
                SatisfactionChanged?.Invoke(player, oldSatisfaction, newSatisfaction);
            }
        }
    }
}
