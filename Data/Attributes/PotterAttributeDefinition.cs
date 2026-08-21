using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class PotterAttributeModifierDefinition : CollectionUnlockedAttributeModifierDefinition<PotterAttributeModifierDefinition, PotterAttributeModifierProgressData>, IConstructable<PotterAttributeModifierDefinition, PotterAttributeModifierProgressData>
    {
        public static PotterAttributeModifierProgressData Create(PotterAttributeModifierDefinition def)
        {
            return new PotterAttributeModifierProgressData(def);
        }
    }

    public class PotterAttributeModifierProgressData(PotterAttributeModifierDefinition def) : CollectionUnlockedAttributeModifierProgressData<PotterAttributeModifierDefinition, PotterAttributeModifierProgressData>(def)
    {
    }
}
