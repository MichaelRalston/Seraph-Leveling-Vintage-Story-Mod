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
                TraitDefinitions.Focused,
                TraitDefinitions.Resourceful,
                TraitDefinitions.Fleetfooted,
                TraitDefinitions.Bowyer,
                TraitDefinitions.Farsighted,
                TraitDefinitions.Claustrophobic
            ]
        };

        public static readonly CharacterClassDefinition Malefactor = new()
        {
            Id = "malefactor",
            Traits = [
                TraitDefinitions.Forager,
                TraitDefinitions.Pilferer,
                TraitDefinitions.Furtive,
                TraitDefinitions.Improviser,
                TraitDefinitions.Frail,
                TraitDefinitions.Nervous
            ]
        };

        public static readonly CharacterClassDefinition Clockmaker = new()
        {
            Id = "clockmaker",
            Traits = [
                TraitDefinitions.Precise,
                TraitDefinitions.Technical,
                TraitDefinitions.Fleetfooted,
                TraitDefinitions.Tinkerer,
                TraitDefinitions.Frail,
                TraitDefinitions.Nervous
            ]
        };

        public static readonly CharacterClassDefinition Blackguard = new()
        {
            Id = "blackguard",
            Traits = [
                TraitDefinitions.Soldier,
                TraitDefinitions.Hardy,
                TraitDefinitions.Merciless,
                TraitDefinitions.Ravenous,
                TraitDefinitions.Nearsighted,
                TraitDefinitions.Heavyhanded
            ]
        };

        public static readonly CharacterClassDefinition Tailor = new()
        {
            Id = "tailor",
            Traits = [
                TraitDefinitions.Clothier,
                TraitDefinitions.Mender,
                TraitDefinitions.Civil,
                TraitDefinitions.Weak,
                TraitDefinitions.Kind
            ]
        };

        public static readonly CharacterClassDefinition VanillaDummy = new()
        {
            Id = "vanilladummy",
            Traits = [
                TraitDefinitions.HungerMastery,
            ]
        };

        // =========================================================================
        // SACREDLIB CLASSES
        // =========================================================================

        public static readonly CharacterClassDefinition Woodsman = new()
        {
            Id = "woodsman",
            Traits = [
                TraitDefinitions.Carpenter,
                TraitDefinitions.Lumberjack,
                TraitDefinitions.TreeWhisperer,
                TraitDefinitions.HeavyFooted
            ]
        };

        public static readonly CharacterClassDefinition Craftsman = new()
        {
            Id = "craftsman",
            Traits = [
                TraitDefinitions.Mason,
                TraitDefinitions.InteriorDesigner,
                TraitDefinitions.Potter,
                TraitDefinitions.Technician,
                TraitDefinitions.SiltSeeker,
                TraitDefinitions.Townie,
                TraitDefinitions.Agoraphobic
            ]
        };

        public static readonly CharacterClassDefinition Witch = new()
        {
            Id = "witch",
            Traits = [
                TraitDefinitions.Alchemist,
                TraitDefinitions.Propagator,
                TraitDefinitions.Naturalist,
                TraitDefinitions.Medic,
                TraitDefinitions.Claustrophobic
            ]
        };

        public static readonly CharacterClassDefinition Blacksmith = new()
        {
            Id = "blacksmith",
            Traits = [
                TraitDefinitions.MasterCraftsman,
                TraitDefinitions.Blacksmith,
                TraitDefinitions.Armorer,
                TraitDefinitions.Heavyhanded
            ]
        };

        public static readonly CharacterClassDefinition Artificer = new()
        {
            Id = "artificer",
            Traits = [
                TraitDefinitions.Technician,
                TraitDefinitions.Tinkerer,
                TraitDefinitions.Pilferer,
                TraitDefinitions.Engineer,
                TraitDefinitions.Technical,
                TraitDefinitions.Weak
            ]
        };

        public static readonly CharacterClassDefinition Miner = new()
        {
            Id = "miner",
            Traits = [
                TraitDefinitions.Detonator,
                TraitDefinitions.Stonespeaker,
                TraitDefinitions.CaveExplorer,
                TraitDefinitions.Nearsighted
            ]
        };

        public static readonly CharacterClassDefinition Homesteader = new()
        {
            Id = "homesteader",
            Traits = [
                TraitDefinitions.Propagator,
                TraitDefinitions.EarthSinger,
                TraitDefinitions.Naturalist,
                TraitDefinitions.Rancher,
                TraitDefinitions.Claustrophobic
            ]
        };

        public static readonly CharacterClassDefinition Huntsman = new()
        {
            Id = "huntsman",
            Traits = [
                TraitDefinitions.Bowyer,
                TraitDefinitions.WildernessExplorer,
                TraitDefinitions.Butcher,
                TraitDefinitions.Ranger,
                TraitDefinitions.WellAdjusted,
                TraitDefinitions.Claustrophobic
            ]
        };

        public static readonly CharacterClassDefinition Guardsman = new()
        {
            Id = "guardsman",
            Traits = [
                TraitDefinitions.Merciless,
                TraitDefinitions.Bulwark,
                TraitDefinitions.ArmyMedic,
                TraitDefinitions.StrongArmed,
                TraitDefinitions.HeavyHands,
                TraitDefinitions.Ravenous
            ]
        };

        public static readonly CharacterClassDefinition Hearthmaster = new()
        {
            Id = "hearthmaster",
            Traits = [
                TraitDefinitions.Culinary,
                TraitDefinitions.Butcher,
                TraitDefinitions.Allumette,
                TraitDefinitions.Agoraphobic
            ]
        };

        public static readonly CharacterClassDefinition Haberdasher = new()
        {
            Id = "haberdasher",
            Traits = [
                TraitDefinitions.Clothier,
                TraitDefinitions.Weaver,
                TraitDefinitions.Townie,
                TraitDefinitions.Agoraphobic
            ]
        };

        public static readonly CharacterClassDefinition Zealot = new()
        {
            Id = "zealot",
            Traits = [
                TraitDefinitions.Sacrificial,
                TraitDefinitions.Insane,
                TraitDefinitions.Nudist
            ]
        };

    }
}
