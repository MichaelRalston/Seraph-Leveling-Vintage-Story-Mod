namespace SeraphLeveling.Data.Attributes
{
    public class SmithCollectionUnlockedAttributeModifierDefinition : BaseCollectionUnlockedAttributeModifierDefinition<SmithCollectionUnlockedAttributeModifierDefinition, SmithCollectionUnlockedAttributeModifierProgressData>, IConstructable<SmithCollectionUnlockedAttributeModifierDefinition, SmithCollectionUnlockedAttributeModifierProgressData>
    {
        public static SmithCollectionUnlockedAttributeModifierProgressData Create(SmithCollectionUnlockedAttributeModifierDefinition def)
        {
            return new SmithCollectionUnlockedAttributeModifierProgressData(def);
        }
    }

    public class SmithCollectionUnlockedAttributeModifierProgressData(SmithCollectionUnlockedAttributeModifierDefinition def) : BaseCollectionUnlockedAttributeModifierProgressData<SmithCollectionUnlockedAttributeModifierDefinition, SmithCollectionUnlockedAttributeModifierProgressData>(def)
    {
    }
}
