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
                TraitDefinitions.LactoseEnthusiast,
                TraitDefinitions.Sacrificial,
                TraitDefinitions.Insane,
                TraitDefinitions.Nudist
            ]
        };

        public static readonly CharacterClassDefinition SacredClassesDummy = new()
        {
            Id = "sacredclassesdummy",
            Traits = [
                TraitDefinitions.HungerMastery,
                TraitDefinitions.Focused,
                TraitDefinitions.Fleetfooted,
                TraitDefinitions.Forager,
                TraitDefinitions.Soldier,
                TraitDefinitions.Hardy,
                TraitDefinitions.Mender,
                TraitDefinitions.Improviser,
            ]
        };

        public static readonly CharacterClassDefinition RustMage = new()
        {
            Id = "rustmage",
            Traits = [
                TraitDefinitions.Enlightenment,
                TraitDefinitions.Gearcalling,
                TraitDefinitions.BetweenWorlds,
                TraitDefinitions.EssenceFed,
                TraitDefinitions.Fractured,
                TraitDefinitions.RustTouch,
            ]
        };

        public static readonly CharacterClassDefinition Timberborn = new()
        {
            Id = "timberborn",
            Traits = [

            ]
        };

        public static readonly CharacterClassDefinition Shadowstrider = new()
        {
            Id = "shadowstrider",
            Traits = []
        };

        public static readonly CharacterClassDefinition Deepforger = new()
        {
            Id = "deepforger",
            Traits = []
        };

        public static readonly CharacterClassDefinition Earthshaper = new()
        {
            Id = "earthshaper",
            Traits = []
        };

        public static readonly CharacterClassDefinition Mason = new()
        {
            Id = "mason",
            Traits = []
        };

        public static readonly CharacterClassDefinition Threadweaver = new()
        {
            Id = "threadweaver",
            Traits = []
        };

        public static readonly CharacterClassDefinition Entropic = new()
        {
            Id = "entropic",
            Traits = []
        };

        public static readonly CharacterClassDefinition Mercenary = new()
        {
            Id = "mercenary",
            Traits = []
        };

        public static readonly CharacterClassDefinition Wildrunner = new()
        {
            Id = "wildrunner",
            Traits = []
        };

        public static readonly CharacterClassDefinition Squire = new()
        {
            Id = "squire",
            Traits = []
        };

        public static readonly CharacterClassDefinition Druid = new()
        {
            Id = "druid",
            Traits = []
        };

        public static readonly CharacterClassDefinition Handwright = new()
        {
            Id = "handwright",
            Traits = []
        };

        public static readonly CharacterClassDefinition Innkeeper = new()
        {
            Id = "innkeeper",
            Traits = []
        };

        public static readonly CharacterClassDefinition Wanderer = new()
        {
            Id = "wanderer",
            Traits = []
        };

        public static readonly CharacterClassDefinition Carpenter = new()
        {
            Id = "carpenter",
            Traits = []
        };

        public static readonly CharacterClassDefinition Potter = new()
        {
            Id = "potter",
            Traits = []
        };

        public static readonly CharacterClassDefinition AldiBlacksmith = new()
        {
            Id = "blacksmith",
            Traits = []
        };

        public static readonly CharacterClassDefinition Lumberjack = new()
        {
            Id = "lumberjack",
            Traits = []
        };

        public static readonly CharacterClassDefinition Farmer = new()
        {
            Id = "farmer",
            Traits = [
                TraitDefinitions.Forager,
                TraitDefinitions.Improviser,
                TraitDefinitions.Nervous,
                TraitDefinitions.Claustrophobic,
                TraitDefinitions.Farmer,
                TraitDefinitions.AnimalFriend,
                TraitDefinitions.StrongShoulders,
                TraitDefinitions.Preserver,
                TraitDefinitions.Saddler,
                TraitDefinitions.GreenFingered,
            ]
        };

        public static readonly CharacterClassDefinition AldiMiner = new()
        {
            Id = "miner",
            Traits = [
                TraitDefinitions.MountainEater,
                // TraitDefinitions.Prospector, -- NOT YET IMPLEMENTED.
                TraitDefinitions.Tough,
                TraitDefinitions.Myopic,
                TraitDefinitions.Heavyhanded
            ]
        };


    }
}
