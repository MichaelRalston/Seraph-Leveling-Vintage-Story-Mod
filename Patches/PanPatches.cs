using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using HarmonyLib;

namespace SeraphLeveling.Patches
{
    [HarmonyPatch(typeof(BlockPan))]
    [HarmonyPatch("CreateDrop")]
    public static class BlockPan_CreateDrop_Patch
    {
        public static void Postfix(EntityAgent byEntity, string fromBlockCode)
        {
            if (byEntity is EntityPlayer playerEntity)
            {
                if (playerEntity.Player is IServerPlayer player)
                {
                    AttributeModifierDefinitions.Prospector.AddCredits(player, 1f);
                }
            }
        }
    }
}
