using System;

namespace SeraphLeveling
{
    public class AttributeModifierProgressData
    {
        protected AttributeModifierDefinition Definition { get; init; }
        protected byte Version { get; init; }

        public AttributeModifierProgressData(AttributeModifierDefinition definition, byte version)
        {
            Definition = definition;
            Version = version;
        }
    }
}