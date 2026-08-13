using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public record class ClothierAttributeModifierDefinition : CollectionUnlockedAttributeModifierDefinition<ClothierAttributeModifierDefinition, ClothierAttributeModifierProgressData>, IConstructable<ClothierAttributeModifierDefinition, ClothierAttributeModifierProgressData>
    {
        public static ClothierAttributeModifierProgressData Create(ClothierAttributeModifierDefinition def)
        {
            return new ClothierAttributeModifierProgressData(def);
        }

        protected override bool IsItemValid(string itemCode)
        {
            return SeraphLevelingModSystem.IsClothingItem(itemCode);
        }
    }

    public class ClothierAttributeModifierProgressData(ClothierAttributeModifierDefinition def) : CollectionUnlockedAttributeModifierProgressData<ClothierAttributeModifierDefinition, ClothierAttributeModifierProgressData>(def)
    {
    }
}
