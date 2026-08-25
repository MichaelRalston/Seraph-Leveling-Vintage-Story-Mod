using Vintagestory.API.Common;
using SeraphLeveling.Util;
using static SeraphLeveling.Util.IAssetLocationMatcher;

namespace SeraphLeveling.Data.Tools
{
    public static class ToolDefinitions
    {
        public static readonly ToolDefinition Pickaxe = new("pickaxe", EnumTool.Pickaxe);
        public static readonly ToolDefinition Axe = new("axe", EnumTool.Axe);
        public static readonly ToolDefinition Shovel = new("shovel", EnumTool.Shovel);
        public static readonly ToolDefinition Weapon = new("weapon", [EnumTool.Axe, EnumTool.Bow, EnumTool.Club, EnumTool.Crossbow, EnumTool.Firearm, EnumTool.Halberd, EnumTool.Hammer, EnumTool.Javelin, EnumTool.Knife, EnumTool.Mace, EnumTool.Pike, EnumTool.Polearm, EnumTool.Poleaxe, EnumTool.Scythe, EnumTool.Sickle, EnumTool.Sling, EnumTool.Spear, EnumTool.Staff, EnumTool.Sword, EnumTool.Warhammer])
        {
            MatchOverride = Or(Simple("axe"), Simple("bow"), Simple("club"), Simple("crossbow"), Simple("firearm"), Simple("halberd"), Simple("hammer"), Simple("javelin"), Simple("knife"), Simple("mace"), Simple("pike"), Simple("polearm"), Simple("poleaxe"), Simple("scythe"), Simple("sickle"), Simple("sling"), Simple("spear"), Simple("staff"), Simple("sword"), Simple("warhammer"))
        };
        public static readonly ToolDefinition Armor = new("armor");
        public static readonly ToolDefinition Poultice = new("poultice");
        public static readonly ToolDefinition Bow = new("bow", EnumTool.Bow);
        public static readonly ToolDefinition Hoe = new("hoe", EnumTool.Hoe);
        public static readonly ToolDefinition Scythe = new("scythe", EnumTool.Scythe);
        public static readonly ToolDefinition Hammer = new("hammer", EnumTool.Hammer);
        public static readonly ToolDefinition Knife = new("knife", EnumTool.Knife);
        public static readonly ToolDefinition Cleaver = new("cleaver", EnumTool.Knife); // Cleaver tools are defined in other mods, not the base game, so treat them like a knife here
        public static readonly ToolDefinition Shears = new("shears", EnumTool.Shears);
        public static readonly ToolDefinition MeleeWeapon = new("melee weapon", [EnumTool.Axe, EnumTool.Club, EnumTool.Halberd, EnumTool.Hammer, EnumTool.Knife, EnumTool.Mace, EnumTool.Pike, EnumTool.Polearm, EnumTool.Poleaxe, EnumTool.Scythe, EnumTool.Sickle, EnumTool.Spear, EnumTool.Staff, EnumTool.Sword, EnumTool.Warhammer])
        {
            MatchOverride = Or(Simple("axe"), Simple("club"), Simple("halberd"), Simple("hammer"), Simple("knife"), Simple("mace"), Simple("pike"), Simple("polearm"), Simple("poleaxe"), Simple("scythe"), Simple("sickle"), Simple("spear"), Simple("staff"), Simple("sword"), Simple("warhammer"))
        };
        public static readonly ToolDefinition RangedWeapon = new("ranged weapon", [EnumTool.Bow, EnumTool.Crossbow, EnumTool.Firearm, EnumTool.Javelin, EnumTool.Sling, EnumTool.Spear])
        {
            MatchOverride = Or(Simple("bow"), Simple("crossbow"), Simple("firearm"), Simple("javelin"), Simple("sling"), Simple("spear"))
        };
        public static readonly ToolDefinition Sling = new("sling", EnumTool.Sling);
        public static readonly ToolDefinition Stone = new("stone")
        {
            MatchOverride = Or(Simple("stone-", MatcherType.PathContains), Simple("stone"), Simple("thrownstone", MatcherType.PathContains), And(Simple("stone", MatcherType.PathContains), Not(Simple("whetstone", MatcherType.PathContains))))
        };
    }
}
