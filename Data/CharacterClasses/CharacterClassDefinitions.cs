using System;
using SeraphLeveling.Data.Traits;

namespace SeraphLeveling.Data.CharacterClasses
{
    public static class CharacterClassDefinitions
    {
        // =========================================================================
        // VANILLA CLASSES
        // =========================================================================

        public static readonly CharacterClassDefinition Commoner = new()
        {
            Id = "commoner",
            Traits = []
        };

        public static readonly CharacterClassDefinition Hunter = new()
        {
            Id = "hunter",
            Traits = [
                TraitDefinitions.Fleetfooted
            ]
        };

        public static readonly CharacterClassDefinition Malefactor = new()
        {
            Id = "malefactor",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Clockmaker = new()
        {
            Id = "clockmaker",
            Traits = [
                TraitDefinitions.Tinkerer
            ]
        };

        public static readonly CharacterClassDefinition Blackguard = new()
        {
            Id = "blackguard",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Tailor = new()
        {
            Id = "tailor",
            Traits = [
            ]
        };

        // =========================================================================
        // SACREDLIB CLASSES
        // =========================================================================

        public static readonly CharacterClassDefinition Woodsman = new()
        {
            Id = "woodsman",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Craftsman = new()
        {
            Id = "craftsman",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Witch = new()
        {
            Id = "witch",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Blacksmith = new()
        {
            Id = "blacksmith",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Artificer = new()
        {
            Id = "artificer",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Miner = new()
        {
            Id = "miner",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Homesteader = new()
        {
            Id = "homesteader",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Huntsman = new()
        {
            Id = "huntsman",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Guardsman = new()
        {
            Id = "guardsman",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Hearthmaster = new()
        {
            Id = "hearthmaster",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Haberdasher = new()
        {
            Id = "haberdasher",
            Traits = [
            ]
        };

        public static readonly CharacterClassDefinition Zealot = new()
        {
            Id = "zealot",
            Traits = [
            ]
        };

    }
}
