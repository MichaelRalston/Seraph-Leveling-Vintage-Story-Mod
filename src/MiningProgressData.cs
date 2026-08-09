using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using Vintagestory.API.Common;
using Vintagestory.API.Server;


namespace SeraphLeveling
{
    /// <summary>
    /// Tracks progress for a specific pickaxe type.
    /// Each pickaxe type has its own increment counter that persists.
    /// </summary>
    public class PickaxeProgressData: IDeepCopyable<PickaxeProgressData>
    {
        /// <summary>Points accumulated toward the next credit with this pickaxe.</summary>
        public int BlocksInIncrement { get; set; }

        /// <summary>Points needed for the next credit with this pickaxe (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public PickaxeProgressData()
        {
            BlocksInIncrement = 0;
            CurrentIncrementSize = SeraphLevelingModSystem.BaseBlocksPerIncrement; // Base increment size
        }

        public PickaxeProgressData Clone()
        {
            return new PickaxeProgressData
            {
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking mining progression with per-pickaxe progress.
    /// Each pickaxe type remembers its own increment counter, encouraging use of many pickaxe types.
    /// </summary>
    public class MiningProgressData: LeveledToolTraitProgressData<MiningProgressData, PickaxeProgressData>, IProgressDataContract<MiningProgressData>, ILeveledTraitContract<MiningProgressData>
    {
        public static int GlobalMax
        {
            get => SeraphLevelingModSystem.MaxMiningSpeedPercent;
            set => SeraphLevelingModSystem.MaxMiningSpeedPercent = value;
        }


        public static string GetHeaderString()
        {
            return "SIT";
        }

        public new static byte GetVersion() {
            return (byte)4;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(LastActivityDay);

            // Snapshot inner dictionary to avoid concurrent modification
            var toolSnapshot = ToolProgress.ToArray();
            writer.Write(toolSnapshot.Length);
            foreach (var toolKvp in toolSnapshot)
            {
                writer.Write(toolKvp.Key); // Pickaxe code
                writer.Write(toolKvp.Value.BlocksInIncrement);
                writer.Write(toolKvp.Value.CurrentIncrementSize);
            }
        }

        public static string SAVE_KEY => "sitMiningProgress";
        public static string Description => "mining";

        public new static MiningProgressData ReadVersion(byte version, BinaryReader reader) {
            MiningProgressData progress;
            int pickaxeCount;
            switch (version) {
                case 1:
                    long blocksMined = reader.ReadInt64();

                    // Convert old blocks to credits using legacy formula
                    int legacyLevel = 0;
                    if (blocksMined >= 100)
                    {
                        double discriminant = 1.0 + (8.0 * blocksMined / 100);
                        legacyLevel = (int)((-1.0 + Math.Sqrt(discriminant)) / 2.0);
                    }

                    return new MiningProgressData
                    {
                        TotalCredits = Math.Min(legacyLevel, SeraphLevelingModSystem.MaxMiningSpeedPercent)
                    };
                case 2:
                    int totalCredits = reader.ReadInt32();
                    string currentPickaxeCode = reader.ReadString();
                    int blocksInIncrement = reader.ReadInt32();
                    int currentIncrementSize = reader.ReadInt32();

                    progress = new MiningProgressData
                    {
                        TotalCredits = totalCredits
                    };

                    // Migrate single pickaxe progress if it exists
                    if (!string.IsNullOrEmpty(currentPickaxeCode))
                    {
                        progress.ToolProgress[currentPickaxeCode] = new PickaxeProgressData
                        {
                            BlocksInIncrement = blocksInIncrement,
                            CurrentIncrementSize = currentIncrementSize
                        };
                    }
                    return progress;
                case 3:
                    progress = new MiningProgressData
                    {
                        TotalCredits = reader.ReadInt32()
                    };

                    pickaxeCount = reader.ReadInt32();
                    for (int j = 0; j < pickaxeCount; j++)
                    {
                        string pickaxeCode = reader.ReadString();
                        var pickaxeProgress = new PickaxeProgressData
                        {
                            BlocksInIncrement = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.ToolProgress[pickaxeCode] = pickaxeProgress;
                    }
                    return progress;
                case 4:
                    progress = new MiningProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };

                    pickaxeCount = reader.ReadInt32();
                    for (int j = 0; j < pickaxeCount; j++)
                    {
                        string pickaxeCode = reader.ReadString();
                        var pickaxeProgress = new PickaxeProgressData
                        {
                            BlocksInIncrement = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        progress.ToolProgress[pickaxeCode] = pickaxeProgress;
                    }
                    return progress;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
        public static void MarkForSave() {
            SeraphLevelingModSystem.pendingMiningProgressSave = true;
        }
        public static ref ConcurrentDictionary<string, MiningProgressData> ProgressDictionary() {
            return ref SeraphLevelingModSystem.MiningProgress;
        }

        public override int GetMaxCredits(EntityPlayer player)
        {
            var cache = SeraphLevelingModSystem.GetCachedTraits(player.PlayerUID);
            bool hasVanillaHardy = cache?.HasHardy ?? SeraphLevelingModSystem.PlayerHasVanillaHardy(player);
            int vanillaHardyBonus = hasVanillaHardy ? SeraphLevelingModSystem.VANILLA_HARDY_MINING_BONUS : 0;
            return GlobalMax - vanillaHardyBonus;
        }

        public override int CalculateBonus(EntityPlayer entity)
        {
            return Math.Min(TotalCredits, GlobalMax);
        }

        public override int ApplyBonus(IServerPlayer player)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = SeraphLevelingModSystem.GetCachedTraits(player.PlayerUID);
            bool hasVanillaHardy = cache?.HasHardy ?? SeraphLevelingModSystem.PlayerHasVanillaHardy(player.Entity);
            bool hasWeak = cache?.HasWeak ?? SeraphLevelingModSystem.PlayerHasVanillaWeak(player.Entity);
            bool hasClaustrophobic = cache?.HasClaustrophobic ?? SeraphLevelingModSystem.PlayerHasVanillaClaustrophobic(player.Entity);

            // Calculate remaining negative trait penalties
            int weakMiningRemaining = hasWeak ? SeraphLevelingModSystem.CalculateRemainingPenalty(SeraphLevelingModSystem.VANILLA_WEAK_MINING_PENALTY, TotalCredits) : 0;
            // HP penalty is tied to mining penalty - when mining penalty is cancelled (at level 10), HP is also cancelled
            int weakHpRemaining = weakMiningRemaining > 0 ? SeraphLevelingModSystem.VANILLA_WEAK_HP_PENALTY : 0;
            int claustrophobicMiningRemaining = hasClaustrophobic ? SeraphLevelingModSystem.CalculateRemainingPenalty(SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_MINING_PENALTY, TotalCredits) : 0;
            // Ore penalty is tied to mining penalty - when mining penalty is cancelled (at level 10), ore is also cancelled
            int claustrophobicOreRemaining = claustrophobicMiningRemaining > 0 ? SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_ORE_PENALTY : 0;

            // Calculate net bonus after cancelling negative traits
            // Negative trait penalty must be fully cancelled before bonus starts showing
            int totalNegativePenalty = 0;
            if (hasWeak) totalNegativePenalty += SeraphLevelingModSystem.VANILLA_WEAK_MINING_PENALTY;
            if (hasClaustrophobic) totalNegativePenalty += SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_MINING_PENALTY;

            int netLevel = Math.Max(0, TotalCredits - totalNegativePenalty);

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMiningSpeedPercent
            int maxEarnableBonus = GetMaxCredits(player.Entity);
            int bonusPercent = Math.Min(netLevel, Math.Max(0, maxEarnableBonus));

            float bonus = bonusPercent * 0.01f;

            // Always apply stats (they're not persistent)
            // Set the mining speed stat
            player.Entity.Stats.Set("miningSpeedMul", SeraphLevelingModSystem.MINING_STAT_CODE, bonus, false);

            // Counter-stats: when a vanilla negative trait's mining penalty is fully cancelled
            // (remaining == 0), apply a +penalty counter on the same stat so the ACTUAL applied
            // mining speed matches the displayed value. Without this, Hunter (Claustrophobic)
            // and Tailor (Weak) would land at a functional +40% mining at maxall (vanilla -10%
            // still applied, our +50% on top, net +40%) while their displayed +50% suggests
            // parity with other classes.
            if (hasClaustrophobic)
            {
                if (claustrophobicMiningRemaining == 0)
                {
                    // Negate the -10% mining speed penalty by applying +10%
                    player.Entity.Stats.Set("miningSpeedMul", "sitClaustrophobicMiningCancel", SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_MINING_PENALTY * 0.01f, false);
                    // Negate the -15% ore drop penalty by applying +15%
                    player.Entity.Stats.Set("oreDropRate", "sitClaustrophobicOreCancel", SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_ORE_PENALTY * 0.01f, false);
                }
                else
                {
                    player.Entity.Stats.Remove("miningSpeedMul", "sitClaustrophobicMiningCancel");
                    player.Entity.Stats.Remove("oreDropRate", "sitClaustrophobicOreCancel");
                }
            }

            // When Weak mining penalty is fully cancelled, also negate the HP penalty AND the mining speed penalty
            if (hasWeak)
            {
                if (weakMiningRemaining == 0)
                {
                    // Negate the -2 HP penalty by applying +2 HP
                    player.Entity.Stats.Set("maxhealthExtraPoints", SeraphLevelingModSystem.WEAK_HP_CANCEL_STAT_CODE, SeraphLevelingModSystem.VANILLA_WEAK_HP_PENALTY, false);
                    // Negate the -10% mining speed penalty by applying +10%
                    player.Entity.Stats.Set("miningSpeedMul", "sitWeakMiningCancel", SeraphLevelingModSystem.VANILLA_WEAK_MINING_PENALTY * 0.01f, false);
                }
                else
                {
                    player.Entity.Stats.Remove("maxhealthExtraPoints", SeraphLevelingModSystem.WEAK_HP_CANCEL_STAT_CODE);
                    player.Entity.Stats.Remove("miningSpeedMul", "sitWeakMiningCancel");
                }
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_MINING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_MINING_BONUS, -1);
            int oldClaustoMining = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, -1);

            bool valuesChanged = (oldLevel != TotalCredits) || (oldBonus != bonusPercent) || (oldClaustoMining != claustrophobicMiningRemaining);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_MINING_LEVEL, TotalCredits);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_MINING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaHardy", hasVanillaHardy);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasWeak", hasWeak);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_WEAK_MINING_REMAINING, weakMiningRemaining);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_WEAK_HP_REMAINING, weakHpRemaining);
                watchedAttrs.SetBool("sitHasClaustrophobic", hasClaustrophobic);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, claustrophobicMiningRemaining);
                watchedAttrs.SetInt(SeraphLevelingModSystem.WATCHED_CLAUSTROPHOBIC_ORE_REMAINING, claustrophobicOreRemaining);

                // Add our trait to extraTraits only if:
                // - Player doesn't already have Hardy AND
                // - All negative mining penalties are cancelled (bonusPercent > 0)
                SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, SeraphLevelingModSystem.MINING_TRAIT_CODE, bonusPercent > 0 && !hasVanillaHardy);

                // Only call MarkPathDirty once at the end (batched update)
                watchedAttrs.MarkPathDirty(SeraphLevelingModSystem.WATCHED_MINING_LEVEL);
            }

            return bonusPercent;
        }
    }
}