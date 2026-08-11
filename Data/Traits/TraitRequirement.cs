using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Traits
{
    public interface IRequiredAttribute {
        public bool IsMet(IServerPlayer player);
    }

    public class RequiredUnlockedAttribute : IRequiredAttribute
    {
        protected IUnlockedAttributeModifierDefinition Definition { get; init; }

        public RequiredUnlockedAttribute(IUnlockedAttributeModifierDefinition definition)
        {
            Definition = definition;
        }

        public bool IsMet(IServerPlayer player)
        {
            return Definition.IsUnlockedForPlayer(player);
        }
    }

    public class RequiredLeveledAttribute : IRequiredAttribute
    {
        protected ILeveledAttributeModifierDefinition Definition { get; init; }
        protected int RequiredCredits { get; init; }

        public RequiredLeveledAttribute(ILeveledAttributeModifierDefinition definition, int requiredCredits)
        {
            Definition = definition;
            RequiredCredits = requiredCredits;
        }

        public bool IsMet(IServerPlayer player)
        {
            return Definition.IsLeveledForPlayer(player, RequiredCredits);
        }
    }
}
