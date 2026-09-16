using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SeraphLeveling.Patches
{
    public static class ShearsPatches
    {
        public static void ItemShears_breakMultiBlock_Prefix(ItemShears __instance, BlockPos pos, IPlayer plr)
        {
            SeraphLevelingModSystem.OnMultiBlockBreaking(plr, pos);
        }
    }
}
