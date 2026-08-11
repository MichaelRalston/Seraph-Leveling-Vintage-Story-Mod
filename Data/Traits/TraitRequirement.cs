using System.Text;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Traits
{
    public interface IRequiredAttribute {
        public string Name { get; }
        public bool IsMet(IServerPlayer player);

        /// <summary>
        /// Get a status string for the requirement to return when the player uses the /trait command
        /// </summary>
        public abstract void CollectStatus(IPlayer player, StringBuilder sb);
    }

    public class RequiredUnlockedAttribute : IRequiredAttribute
    {
        protected IUnlockedAttributeModifierDefinition Definition { get; init; }
        public string Name { get => Definition.Name; }

        public RequiredUnlockedAttribute(IUnlockedAttributeModifierDefinition definition)
        {
            Definition = definition;
        }

        public bool IsMet(IServerPlayer player)
        {
            return Definition.IsUnlockedForPlayer(player);
        }

        public void CollectStatus(IPlayer player, StringBuilder sb)
        {
            sb.AppendLine($"  {Name}: {(Definition.IsUnlockedForPlayer(player) ? "UNLOCKED ✓" : "Locked ✗")}");
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
        }

        public bool IsMet(IServerPlayer player)
        {
            return Definition.IsLeveledForPlayer(player, RequiredCredits);
        }

        public void CollectStatus(IPlayer player, StringBuilder sb)
        {
            sb.AppendLine($"  {Name} level: {Definition.GetCreditsForPlayer(player)} / {RequiredCredits} ({(Definition.GetCreditsForPlayer(player) >= RequiredCredits ? "✓" : "✗")})");
        }
    }
}
