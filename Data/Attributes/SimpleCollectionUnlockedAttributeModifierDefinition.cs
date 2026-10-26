namespace SeraphLeveling.Data.Attributes
{
    public class SimpleCollectionUnlockedAttributeModifierDefinition : BaseCollectionUnlockedAttributeModifierDefinition<SimpleCollectionUnlockedAttributeModifierDefinition, SimpleCollectionUnlockedAttributeModifierProgressData>, IConstructable<SimpleCollectionUnlockedAttributeModifierDefinition, SimpleCollectionUnlockedAttributeModifierProgressData>
    {
        public static SimpleCollectionUnlockedAttributeModifierProgressData Create(SimpleCollectionUnlockedAttributeModifierDefinition def)
        {
            return new SimpleCollectionUnlockedAttributeModifierProgressData(def);
        }
    }

    public class SimpleCollectionUnlockedAttributeModifierProgressData(SimpleCollectionUnlockedAttributeModifierDefinition def) : BaseCollectionUnlockedAttributeModifierProgressData<SimpleCollectionUnlockedAttributeModifierDefinition, SimpleCollectionUnlockedAttributeModifierProgressData>(def)
    {
    }
}
