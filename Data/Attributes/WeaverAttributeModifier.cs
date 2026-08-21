namespace SeraphLeveling.Data.Attributes
{
    public class WeaverAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<WeaverAttributeModifierDefinition, WeaverAttributeModifierProgressData>, IConstructable<WeaverAttributeModifierDefinition, WeaverAttributeModifierProgressData>
    {
        public static WeaverAttributeModifierProgressData Create(WeaverAttributeModifierDefinition def)
        {
            return new WeaverAttributeModifierProgressData(def);
        }
    }

    public class WeaverAttributeModifierProgressData(WeaverAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<WeaverAttributeModifierDefinition, WeaverAttributeModifierProgressData>(definition)
    {
    }
}
