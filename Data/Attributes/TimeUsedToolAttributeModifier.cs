namespace SeraphLeveling.Data.Attributes
{
    public class TimeUsedToolModifierProgressData(TimeUsedToolModifierDefinition definition) : LeveledToolAttributeModifierProgressData<TimeUsedToolModifierDefinition, TimeUsedToolModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }
    public class TimeUsedToolModifierDefinition : LeveledToolAttributeModifierDefinition<TimeUsedToolModifierDefinition, TimeUsedToolModifierProgressData, SimpleToolProgress>, IConstructable<TimeUsedToolModifierDefinition, TimeUsedToolModifierProgressData>
    {
        public static TimeUsedToolModifierProgressData Create(TimeUsedToolModifierDefinition definition) { return new TimeUsedToolModifierProgressData(definition); }
    }
}
