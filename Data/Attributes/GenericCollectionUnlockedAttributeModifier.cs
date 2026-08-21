using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class GenericCollectionUnlockedAttributeModifierDefinition : CollectionUnlockedAttributeModifierDefinition<GenericCollectionUnlockedAttributeModifierDefinition, GenericCollectionUnlockedAttributeModifierProgressData>, IConstructable<GenericCollectionUnlockedAttributeModifierDefinition, GenericCollectionUnlockedAttributeModifierProgressData>
    {
        public static GenericCollectionUnlockedAttributeModifierProgressData Create(GenericCollectionUnlockedAttributeModifierDefinition def)
        {
            return new GenericCollectionUnlockedAttributeModifierProgressData(def);
        }
    }

    public class GenericCollectionUnlockedAttributeModifierProgressData(GenericCollectionUnlockedAttributeModifierDefinition def) : CollectionUnlockedAttributeModifierProgressData<GenericCollectionUnlockedAttributeModifierDefinition, GenericCollectionUnlockedAttributeModifierProgressData>(def)
    {
    }
}
