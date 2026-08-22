using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Patches
{
    /// <summary>
    /// Server-side Harmony patches for item damage tracking.
    /// </summary>
    public static class ItemDamagePatches
    {
        public static void DamageItem_Prefix(CollectibleObject __instance, IWorldAccessor world, Entity byEntity, ItemSlot itemSlot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            if (__instance.Tool == null) return;
            if (byEntity is not EntityPlayer byPlayer) return;
            if (byPlayer.Player is not IServerPlayer player) return;
            string playerUid = byPlayer.PlayerUID;
            string toolCode = __instance.Code.Path.ToLowerInvariant();
#if SPAMMYDEBUG
            ServerApi.Logger.Debug($"[SeraphLeveling] Checking item damage points for tool code {toolCode}.");
#endif

            switch (__instance.Tool)
            {
                case EnumTool.Knife:
                    AttributeModifierDefinitions.KnifeDurability.GetForPlayer(playerUid).DoEvent(player, toolCode, amount, RepairableToolProgress.Usage);
                    break;
                case EnumTool.Axe:
                    AttributeModifierDefinitions.AxeDurability.GetForPlayer(playerUid).DoEvent(player, toolCode, amount, RepairableToolProgress.Usage);
                    break;
                case EnumTool.Pickaxe:
                    AttributeModifierDefinitions.PickaxeDurability.GetForPlayer(playerUid).DoEvent(player, toolCode, amount, RepairableToolProgress.Usage);
                    break;
                case EnumTool.Scythe:
                    AttributeModifierDefinitions.ScytheDurability.GetForPlayer(playerUid).DoEvent(player, toolCode, amount, RepairableToolProgress.Usage);
                    break;
                case EnumTool.Hammer:
                    AttributeModifierDefinitions.HammerDurability.GetForPlayer(playerUid).DoEvent(player, toolCode, amount, RepairableToolProgress.Usage);
                    break;
                case EnumTool.Hoe:
                    AttributeModifierDefinitions.HoeDurability.GetForPlayer(playerUid).DoEvent(player, toolCode, amount, RepairableToolProgress.Usage);
                    break;
                case EnumTool.Bow:
                    AttributeModifierDefinitions.BowDurability.GetForPlayer(playerUid).DoEvent(player, toolCode, amount, RepairableToolProgress.Usage);
                    break;
            }
        }
        
    }
}
