using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Patches
{
    /// <summary>
    /// Server-side Harmony patches for entity damage tracking.
    /// </summary>
    public static class EntityDamagePatches
    {
        /// <summary>
        /// Postfix for Entity.ReceiveDamage - tracks melee and ranged damage dealt by players,
        /// and damage blocked by armor when players receive damage.
        /// </summary>
        public static void ReceiveDamage_Postfix(Entity __instance, DamageSource damageSource, float damage, bool __result)
        {
            // Debug: Log all damage events to diagnose CO issues (disabled by default to avoid log spam)
            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi?.Logger.Debug($"[SeraphLeveling] ReceiveDamage_Postfix: target={__instance?.Code}, damage={damage}, result={__result}, SourceEntity={damageSource?.SourceEntity?.Code}, CauseEntity={damageSource?.CauseEntity?.Code}, Type={damageSource?.Type}");
            }

            // Only process if damage was actually dealt
            if (!__result || damage <= 0) return;

            // Track armor damage blocked if the entity taking damage is a player wearing armor
            TrackArmorDamageBlocked(__instance, damageSource, damage);

            // Check if this is ranged damage (projectile with CauseEntity)
            if (SeraphLevelingModSystem.IsRangedDamage(damageSource))
            {
                // For ranged: CauseEntity is the shooter, SourceEntity is the projectile
                if (damageSource.CauseEntity is not EntityPlayer shooterEntity) return;

                if (shooterEntity.Player is not IServerPlayer shooterPlayer) return;

                // Don't count self-damage
                if (__instance == shooterEntity) return;

                // Get the weapon combination (bow+arrow, sling+stone, etc.)
                string weaponCombo = SeraphLevelingModSystem.GetRangedWeaponCombo(damageSource.SourceEntity, shooterEntity);

                if (weaponCombo != null)
                {
                    SeraphLevelingModSystem.ProcessDamage(shooterPlayer, __instance, true, weaponCombo, damage);
                }

                // First, check the projectile itself for thrown weapons (javelins, thrown spears)
                // These weapons ARE the projectile, so we detect from SourceEntity
                var projectileCode = damageSource.SourceEntity?.Code;
                if (projectileCode != null && projectileCode.Valid)
                {
                    SeraphLevelingModSystem.ProcessCODamage(shooterPlayer, null, projectileCode, damage);
                }

                // Also check held ranged weapon for bows/crossbows/slings/firearms
                var heldRangedItem = shooterPlayer.Entity?.RightHandItemSlot?.Itemstack?.Collectible;
                if (heldRangedItem != null)
                {
                    SeraphLevelingModSystem.ProcessCODamage(shooterPlayer, true, heldRangedItem.Code, damage);
                }

                return; // Don't also count as melee
            }

            // Check if damage was dealt by a player (melee)
            if ((damageSource?.SourceEntity as EntityPlayer)?.Player is not IServerPlayer attackerPlayer) return;

            // Don't count self-damage
            if (__instance == damageSource.SourceEntity) return;

            // Get held weapon
            var heldItem = attackerPlayer.Entity?.RightHandItemSlot?.Itemstack?.Collectible;
            if (heldItem == null) return;

            var itemCode = heldItem.Code;
            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi?.Logger?.Debug($"[SeraphLeveling] Melee hit with held item: '{itemCode}'");
            }

            string weaponType = SeraphLevelingModSystem.GetWeaponTypeFromCode(itemCode);

            if (weaponType != null)
            {
                SeraphLevelingModSystem.ProcessDamage(attackerPlayer, __instance, false, weaponType, damage);
            }

            // Combat Overhaul: Also track CO melee proficiency if enabled
            SeraphLevelingModSystem.ProcessCODamage(attackerPlayer, false, itemCode, damage);
        }

        /// <summary>
        /// Track damage blocked by armor when a player takes damage.
        /// Uses hit probability (50% body, 30% legs, 20% head) to distribute damage to armor pieces.
        /// </summary>
        private static void TrackArmorDamageBlocked(Entity damagedEntity, DamageSource damageSource, float finalDamage)
        {
            // Only track actual combat damage - filter out healing and non-combat damage types
            if (damageSource == null) return;

            // Filter out non-combat damage types (healing, hunger, suffocation, etc.)
            // Only count damage that armor can actually block: melee attacks and projectiles
            var damageType = damageSource.Type;
            if (damageType == EnumDamageType.Heal ||
                damageType == EnumDamageType.Hunger ||
                damageType == EnumDamageType.Suffocation ||
                damageType == EnumDamageType.Poison ||
                damageType == EnumDamageType.Gravity ||
                damageType == EnumDamageType.Fire ||
                damageType == EnumDamageType.Frost ||
                damageType == EnumDamageType.Heat ||
                damageType == EnumDamageType.Electricity)
            {
                return; // These damage types are not blocked by armor
            }

            // Only process for players
            var playerEntity = damagedEntity as EntityPlayer;
            if (playerEntity == null) return;

            var player = playerEntity.Player as IServerPlayer;
            if (player == null) return;

            // Get the player's armor using character inventory
            var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (characterInventory == null) return;

            // Find armor pieces and calculate damage blocked per piece
            // Use hit probability: 50% body, 30% legs, 20% head
            foreach (var slot in characterInventory)
            {
                if (slot?.Itemstack?.Collectible == null) continue;

                var itemCode = slot.Itemstack.Collectible.Code;
                string armorType = SeraphLevelingModSystem.GetArmorType(itemCode);

                if (armorType == null) continue; // Not armor

                // Get the armor's protection value from item attributes
                // Vintage Story uses protectionModifiers.relativeProtection (0-1 scale, e.g., 0.2 = 20% reduction)
                float relativeProtection = 0f;
                var itemAttributes = slot.Itemstack.Collectible.Attributes;
                if (itemAttributes != null)
                {
                    var protectionModifiers = itemAttributes["protectionModifiers"];
                    if (protectionModifiers != null && protectionModifiers.Exists)
                    {
                        relativeProtection = protectionModifiers["relativeProtection"].AsFloat(0f);
                    }
                }

                // If no protection found, give a minimum credit for wearing armor at all
                // This ensures armor that blocks any damage still gives some XP
                if (relativeProtection <= 0)
                {
                    // Default to a small protection value so armor still grants some XP
                    relativeProtection = 0.05f; // 5% minimum
                }

                // Determine hit probability based on armor slot type (from item code)
                string itemPath = itemCode.Path;
                float hitProbability;
                if (itemPath.Contains("-head-") || itemPath.Contains("-helmet-"))
                    hitProbability = 0.2f;
                else if (itemPath.Contains("-legs-") || itemPath.Contains("-leggings-"))
                    hitProbability = 0.3f;
                else // body
                    hitProbability = 0.5f;

                // Calculate damage blocked by this armor piece
                // For a hit that lands on this piece: originalDamage = finalDamage / (1 - protection)
                // damageBlocked = originalDamage - finalDamage = finalDamage * protection / (1 - protection)
                // We scale by hit probability since not all hits go to this piece
                // relativeProtection is already on 0-1 scale (e.g., 0.2 = 20% reduction)
                float protection = relativeProtection;
                if (protection >= 1f) protection = 0.99f; // Prevent division by zero

                float damageBlocked = finalDamage * protection / (1f - protection) * hitProbability;

                if (damageBlocked > 0)
                {
                    SeraphLevelingModSystem.ProcessArmorDamageBlocked(player, damageBlocked, itemCode);
                }
            }
        }
    }
}
