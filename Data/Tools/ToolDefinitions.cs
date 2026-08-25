using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Tools
{
    public static class ToolDefinitions
    {
        public static readonly ToolDefinition Pickaxe = new("pickaxe", EnumTool.Pickaxe);
        public static readonly ToolDefinition Axe = new("axe", EnumTool.Axe);
        public static readonly ToolDefinition Shovel = new("shovel", EnumTool.Shovel);
        public static readonly ToolDefinition Weapon = new("weapon", [EnumTool.Axe, EnumTool.Bow, EnumTool.Club, EnumTool.Crossbow, EnumTool.Firearm, EnumTool.Halberd, EnumTool.Hammer, EnumTool.Javelin, EnumTool.Knife, EnumTool.Mace, EnumTool.Pike, EnumTool.Polearm, EnumTool.Poleaxe, EnumTool.Scythe, EnumTool.Sickle, EnumTool.Sling, EnumTool.Spear, EnumTool.Staff, EnumTool.Sword, EnumTool.Warhammer]);
        public static readonly ToolDefinition Armor = new("armor");
        public static readonly ToolDefinition Poultice = new("poultice");
        public static readonly ToolDefinition Bow = new("bow", EnumTool.Bow);
        public static readonly ToolDefinition Hoe = new("hoe", EnumTool.Hoe);
        public static readonly ToolDefinition Scythe = new("scythe", EnumTool.Scythe);
        public static readonly ToolDefinition Hammer = new("hammer", EnumTool.Hammer);
        public static readonly ToolDefinition Knife = new("knife", EnumTool.Knife);
        public static readonly ToolDefinition Cleaver = new("cleaver", EnumTool.Knife); // Cleaver tools are defined in other mods, not the base game, so treat them like a knife here
        public static readonly ToolDefinition Shears = new("shears", EnumTool.Shears);
        public static readonly ToolDefinition MeleeWeapon = new("melee weapon", [EnumTool.Axe, EnumTool.Club, EnumTool.Halberd, EnumTool.Hammer, EnumTool.Knife, EnumTool.Mace, EnumTool.Pike, EnumTool.Polearm, EnumTool.Poleaxe, EnumTool.Scythe, EnumTool.Sickle, EnumTool.Spear, EnumTool.Staff, EnumTool.Sword, EnumTool.Warhammer]);
        public static readonly ToolDefinition RangedWeapon = new("ranged weapon", [EnumTool.Bow, EnumTool.Crossbow, EnumTool.Firearm, EnumTool.Javelin, EnumTool.Sling, EnumTool.Spear]);
    }
}
