namespace SeraphLeveling.Data.Attributes
{
    public class InteriorDesignerAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<InteriorDesignerAttributeModifierDefinition, InteriorDesignerAttributeModifierProgressData>, IConstructable<InteriorDesignerAttributeModifierDefinition, InteriorDesignerAttributeModifierProgressData>
    {
        public static InteriorDesignerAttributeModifierProgressData Create(InteriorDesignerAttributeModifierDefinition def)
        {
            return new InteriorDesignerAttributeModifierProgressData(def);
        }
    }

    public class InteriorDesignerAttributeModifierProgressData(InteriorDesignerAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<InteriorDesignerAttributeModifierDefinition, InteriorDesignerAttributeModifierProgressData>(definition)
    {
    }
}
