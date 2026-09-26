using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using SeraphLeveling.Data.Mods;
using HarmonyLib;
using System.Reflection;

namespace SeraphLeveling.Patches
{
    public static class ImmersiveWoodworkingPatches
    {
        public static void SpawnEjectedItem_Prefix(object __instance, ItemStack stack, double alongFrac, double ejectX, double ejectZ, IPlayer byPlayer)
        {
            if (byPlayer is not IServerPlayer player) return;

            CraftingPatches.TriggerGridCraftingResult(player, stack.Collectible.Code, stack.StackSize);
        }

        public static void SpawnDistributed_Prefix(object __instance, ItemStack template, int dropCount, IPlayer byPlayer)
        {
            if (byPlayer is not IServerPlayer player) return;

            CraftingPatches.TriggerGridCraftingResult(player, template.Collectible.Code, dropCount);
        }
    }
}
