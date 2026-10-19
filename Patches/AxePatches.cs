using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Reflection;
using Vintagestory.API.Common.Entities;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace SeraphLeveling.Patches
{
    [HarmonyPatch(typeof(ItemAxe), "OnBlockBrokenWith")]
    public class Patch_ItemAxe_OnBlockBrokenWith
    {
        // Safely look up and cache the private 'FindTree' method info
        private static readonly MethodInfo FindTreeMethod =
            typeof(ItemAxe).GetMethod("FindTree", BindingFlags.Public | BindingFlags.Instance);

        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Harmony Runtime Patch")]
        static void Prefix(ItemAxe __instance, IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel)
        {
            // 1. Only track on the server side where item damage and block breaking are handled
            if (blockSel == null || world.Side != EnumAppSide.Server) return;
            if (byEntity == null)
            {
                return;
            }
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer entityPlayer)
            {
                if (entityPlayer == null)
                {
                    return;
                }
                byPlayer = world.PlayerByUid(entityPlayer.PlayerUID);
            }
            if (byPlayer == null) return;

            // 2. Invoke the private FindTree method on this specific axe instance
            // Pass parameters: (IWorldAccessor world, BlockPos pos)
            object[] invokeArgs = [world, blockSel.Position, 0, 0];
            object rawResult = FindTreeMethod.Invoke(__instance, invokeArgs);
            if (rawResult is IEnumerable treePositions)
            {
                foreach (object obj in treePositions)
                {
                    if (obj is BlockPos pos)
                    {
                        SeraphLevelingModSystem.OnMultiBlockBreaking(byPlayer, pos);
                    }
                }
            }
            else
            {
                SeraphLevelingModSystem.OnMultiBlockBreaking(byPlayer, blockSel.Position);
            }
        }
    }
}
