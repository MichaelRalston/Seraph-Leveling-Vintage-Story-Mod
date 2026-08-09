using System;

namespace SeraphLeveling
{
    public static class ModDefinitions
    {
        public static readonly ModDefinition Vanilla = new()
        {
            ModId = "vanilla",
            CharacterClasses = [
                CharacterClassDefinitions.Commoner,
                CharacterClassDefinitions.Hunter
            ]
        };

        public static readonly ModDefinition SacredClasses = new()
        {
            ModId = "sacredlib",
            CharacterClasses = [
                CharacterClassDefinitions.Commoner
            ]
        };
    }
}
