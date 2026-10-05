using HarmonyLib;
using Vintagestory.API.Common;
using System.Collections.Generic;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Reflection;
using SeraphLeveling.Behaviors;
using System;
using Vintagestory.API.Datastructures;

namespace SeraphLeveling.Patches
{
    public static class CookingPatches
    {
        public static event TriggerCookingResultDelegate TriggerCookingResult;

        [HarmonyPatch]
        public class Patch_Firepit_And_Other_Interaction
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Block), nameof(Block.OnBlockInteractStart));
            }

            [HarmonyPrefix]
            public static void Prefix(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
            {
                if (world.Side == EnumAppSide.Client || blockSel?.Position == null || byPlayer == null) return;

                BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (be != null)
                {
                    var tracker = be.GetBehavior<InteractionTrackingBehavior>();
                    if (tracker != null)
                    {
                        tracker.lastInteractedBy = byPlayer.PlayerUID;
                    }
                }
            }
        }

        [ThreadStatic]
        private static BlockEntity CurrentBakingContext;

        [HarmonyPatch]
        public class Patch_Baking_Step
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BlockEntityOven), "IncrementallyBake");
            }

            [HarmonyPrefix]
            public static void Prefix(BlockEntityOven __instance)
            {
                CurrentBakingContext = __instance;
            }
        }

        [HarmonyPatch]
        public class Patch_Oven_Interaction
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BlockEntityOven), "OnInteract");
            }

            [HarmonyPrefix]
            public static void Prefix(BlockEntity __instance, IPlayer byPlayer)
            {
                if (byPlayer == null || __instance.Api?.Side != EnumAppSide.Server) return;

                var tracker = __instance.GetBehavior<InteractionTrackingBehavior>();
                if (tracker != null)
                {
                    tracker.lastInteractedBy = byPlayer.PlayerUID;
                }
            }
        }

        public class Patch_CollectibleObject_OnBaked
        {
            public static void Postfix(ItemStack oldStack, ItemStack newStack)
            {
                // 1. Enforce authoritative server execution
                ICoreAPI api = SeraphLevelingModSystem.ServerApi;
                if (api?.Side != EnumAppSide.Server) return;

                // 2. Safely locate the active baking inventory hosting this stack

                InventoryBase inventory = null;

                // Check if the source item tracks its container context inside TempAttributes
                if (oldStack?.Attributes != null && oldStack.Attributes.HasAttribute("inventory"))
                {
                    inventory = oldStack.Attributes["inventory"]?.GetValue() as InventoryBase;
                }

                if (inventory == null && newStack?.Attributes != null && newStack.Attributes.HasAttribute("inventory"))
                {
                    inventory = newStack.Attributes["inventory"]?.GetValue() as InventoryBase;
                }

                // 3. Resolve the BlockPos and BlockEntity data structures
                var pos = inventory?.Pos;
                if (pos == null && CurrentBakingContext != null)
                {
                    pos = CurrentBakingContext.Pos;
                }

                if (pos != null)
                {
                    BlockEntity hostingBE = api.World.BlockAccessor.GetBlockEntity(pos);
                    if (hostingBE != null)
                    {
                        // 4. Safely pull your behavior tracking metrics
                        var tracker = hostingBE.GetBehavior<InteractionTrackingBehavior>();
                        if (tracker != null && !string.IsNullOrEmpty(tracker.lastInteractedBy))
                        {
                            if (api.World.PlayerByUid(tracker.lastInteractedBy) is IServerPlayer player)
                            {
                                // 5. Calculate servings or quantities
                                int quantity = newStack?.StackSize ?? oldStack.StackSize;

                                // Trigger your existing event pipeline!
                                TriggerCookingResult?.Invoke(player, newStack.Collectible.Code, quantity);
                            }
                        }
                    }
                }
                else
                {
                    api.Logger.Debug($"[SeraphLeveling] OnBaked triggered, but could not trace parent inventory context; {inventory?.GetType().Name} at {inventory?.Pos}.");
                }
            }
        }
        public class CookingSmeltPatch
        {
            public static void Postfix(CollectibleObject __instance, IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
            {
                if (world?.Side != EnumAppSide.Server || outputSlot?.Itemstack == null) return;

                int portionsCooked = -1;
                ItemStack cookedItem = outputSlot.Itemstack;
                if (__instance is BlockCookingContainer potBlock)
                {
                    ItemStack[] ingredients = potBlock.GetCookingStacks(cookingSlotsProvider);
                    CookingRecipe recipe = potBlock.GetMatchingCookingRecipe(world, ingredients, out portionsCooked);

                    if (recipe != null)
                    {
                        if (!recipe.IsFood)
                        {
                            return;
                        }
                    }
                    if (cookedItem?.Collectible is BlockCookedContainer cookedContainer)
                    {
                        portionsCooked = (int)cookedContainer.GetQuantityServings(world, cookedItem);
                    }
                }
                else
                {
                    var nutrition = cookedItem.Collectible.GetNutritionProperties(world, cookedItem, null);

                    bool isFood = nutrition != null ||
                                  cookedItem.Item?.NutritionProps != null ||
                                  cookedItem.Block?.NutritionProps != null;

                    if (!isFood) return;
                }

                BlockEntity hostingBlockEntity = null;

                if (cookingSlotsProvider is InventoryBase inventory && inventory.Pos != null)
                {
                    hostingBlockEntity = world.BlockAccessor.GetBlockEntity(inventory.Pos);
                }
                else if (cookingSlotsProvider is ItemSlot slot && slot.Inventory?.Pos != null)
                {
                    hostingBlockEntity = world.BlockAccessor.GetBlockEntity(slot.Inventory.Pos);
                }
                else if (cookingSlotsProvider is BlockEntity directBE)
                {
                    hostingBlockEntity = directBE;
                }

                if (hostingBlockEntity == null)
                {
                    return;
                }
                var tracker = hostingBlockEntity.GetBehavior<InteractionTrackingBehavior>();
                if (tracker == null)
                {
                    return;
                }
                string cookUID = tracker.lastInteractedBy;
                if (string.IsNullOrEmpty(cookUID)) return;

                if (world.PlayerByUid(cookUID) is not IServerPlayer player) return;

                if (portionsCooked < 0)
                {
                    if (cookedItem.Attributes.HasAttribute("servings"))
                    {
                        portionsCooked = (int)cookedItem.Attributes.GetFloat("servings");
                    }
                    else
                    {
                        portionsCooked = 1;
                    }
                }

                if (portionsCooked > 0)
                {
                    TriggerCookingResult?.Invoke(player, cookedItem.Collectible.Code, portionsCooked);
                }
            }
        }

    }
}