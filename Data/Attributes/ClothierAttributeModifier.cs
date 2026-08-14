using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class ClothierAttributeModifierDefinition : CollectionUnlockedAttributeModifierDefinition<ClothierAttributeModifierDefinition, ClothierAttributeModifierProgressData>, IConstructable<ClothierAttributeModifierDefinition, ClothierAttributeModifierProgressData>
    {
        public static ClothierAttributeModifierProgressData Create(ClothierAttributeModifierDefinition def)
        {
            return new ClothierAttributeModifierProgressData(def);
        }
    }

    public class ClothierAttributeModifierProgressData(ClothierAttributeModifierDefinition def) : CollectionUnlockedAttributeModifierProgressData<ClothierAttributeModifierDefinition, ClothierAttributeModifierProgressData>(def)
    {
    }
}
