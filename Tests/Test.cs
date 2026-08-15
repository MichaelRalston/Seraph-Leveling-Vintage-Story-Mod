using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Tests
{
// =========================================================================
    // TEST SUITE
    // =========================================================================

    /// <summary>
    /// Result of a single test.
    /// </summary>
    public class TestResult
    {
        public string TestId { get; set; }
        public string Description { get; set; }
        public bool Passed { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
        public string ErrorMessage { get; set; }

        public override string ToString()
        {
            if (Passed)
                return $"  [PASS] {TestId}: {Description}";
            else
                return $"  [FAIL] {TestId}: {Description}\n         Expected: {ExpectedValue}, Got: {ActualValue}" +
                       (string.IsNullOrEmpty(ErrorMessage) ? "" : $"\n         Error: {ErrorMessage}");
        }
    }

    /// <summary>
    /// Test suite for Seraph Leveling mod. Runs automated tests for all trait calculations.
    /// </summary>
    public static class TraitTestSuite
    {
        private static List<TestResult> results;
        private static List<TestResult> allFailedTests;
        private static int passCount;
        private static int failCount;

        /// <summary>
        /// Run all tests or a specific category.
        /// </summary>
        public static string RunTests(string category, IServerPlayer player)
        {
            results = new List<TestResult>();
            allFailedTests = new List<TestResult>();
            passCount = 0;
            failCount = 0;

            var sb = new StringBuilder();
            sb.AppendLine("[SeraphLeveling Tests] Starting test suite...\n");

            bool runAll = string.IsNullOrEmpty(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase);

            if (category != null && category.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("Available test categories:");
                sb.AppendLine("  mining      - Mining calculation tests");
                sb.AppendLine("  melee       - Melee damage calculation tests");
                sb.AppendLine("  armor       - Armor durability and walk speed tests");
                sb.AppendLine("  negative    - Negative trait cancellation tests");
                sb.AppendLine("  detection   - Block, weapon, and armor detection tests");
                sb.AppendLine("  persistence - Data save and load consistency tests");
                sb.AppendLine("  all         - Run all tests");
                return sb.ToString();
            }

            // Run test categories
            if (runAll || (category != null && category.Equals("mining", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running mining tests...");
                RunMiningTests(player.Entity);
                sb.AppendLine(FormatCategoryResults("Mining"));
            }

            if (runAll || (category != null && category.Equals("melee", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running melee tests...");
                RunMeleeTests();
                sb.AppendLine(FormatCategoryResults("Melee"));
            }

            if (runAll || (category != null && category.Equals("armor", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running armor tests...");
                RunArmorTests();
                sb.AppendLine(FormatCategoryResults("Armor"));
            }

            if (runAll || (category != null && category.Equals("negative", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running negative trait tests...");
                RunNegativeTraitTests();
                sb.AppendLine(FormatCategoryResults("Negative Traits"));
            }

            if (runAll || (category != null && category.Equals("detection", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running detection tests...");
                RunDetectionTests();
                sb.AppendLine(FormatCategoryResults("Detection"));
            }

            if (runAll || (category != null && category.Equals("persistence", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running persistence tests...");
                RunPersistenceTests(player);
                sb.AppendLine(FormatCategoryResults("Persistence"));
            }

            // Summary
            sb.AppendLine("\n[SeraphLeveling Tests] === SUMMARY ===");
            sb.AppendLine($"  TOTAL: {passCount}/{passCount + failCount} passed ({(passCount + failCount > 0 ? (passCount * 100 / (passCount + failCount)) : 0)}%)");

            if (failCount > 0 && allFailedTests.Count > 0)
            {
                sb.AppendLine("\nFailed tests:");
                foreach (var result in allFailedTests)
                {
                    sb.AppendLine(result.ToString());
                }
            }

            return sb.ToString();
        }

        private static string FormatCategoryResults(string category)
        {
            int catPass = results.Count(r => r.Passed);
            int catFail = results.Count(r => !r.Passed);
            int catTotal = results.Count;
            string result = $"  {category}: {catPass}/{catTotal} passed";

            // Save failed tests before clearing
            allFailedTests.AddRange(results.Where(r => !r.Passed));

            // Update totals
            passCount += catPass;
            failCount += catFail;

            // Reset for next category
            results.Clear();

            return result;
        }

        // =========================================================================
        // ASSERTION HELPERS
        // =========================================================================

        private static void AssertEqual<T>(string testId, string desc, T expected, T actual)
        {
            bool passed = EqualityComparer<T>.Default.Equals(expected, actual);
            results.Add(new TestResult
            {
                TestId = testId,
                Description = desc,
                Passed = passed,
                ExpectedValue = expected?.ToString() ?? "null",
                ActualValue = actual?.ToString() ?? "null"
            });
        }

        private static void AssertTrue(string testId, string desc, bool condition, string expectedDesc = "true", string actualDesc = "false")
        {
            results.Add(new TestResult
            {
                TestId = testId,
                Description = desc,
                Passed = condition,
                ExpectedValue = expectedDesc,
                ActualValue = condition ? expectedDesc : actualDesc
            });
        }

        private static void AssertInRange(string testId, string desc, int value, int min, int max)
        {
            bool passed = value >= min && value <= max;
            results.Add(new TestResult
            {
                TestId = testId,
                Description = desc,
                Passed = passed,
                ExpectedValue = $"{min}-{max}",
                ActualValue = value.ToString()
            });
        }

        // =========================================================================
        // MINING TESTS
        // =========================================================================

        private static void RunMiningTests(EntityPlayer player)
        {
            int maxMining = AttributeModifierDefinitions.MiningSpeed.GlobalMaxCredits;
            var progress = AttributeModifierDefinitions.MiningSpeed.CreateProgressData();
            progress.TotalCredits = 1;

            // MINE-001: First credit at base increment
            // With default settings: 100 blocks = 1 credit = 1%
            AssertEqual("MINE-001", "Mining bonus percent at 1 credit", 1, AttributeModifierDefinitions.MiningSpeed.CalculateBonus(player, progress));
            progress.TotalCredits = maxMining+50;

            // MINE-002: Credits capped at configured max
            AssertEqual("MINE-002", $"Mining bonus capped at max ({maxMining}%)", maxMining, AttributeModifierDefinitions.MiningSpeed.CalculateBonus(player, progress));
            progress.TotalCredits = 0;

            // MINE-003: Zero credits yields zero bonus
            AssertEqual("MINE-003", "Mining bonus at 0 credits", 0, AttributeModifierDefinitions.MiningSpeed.CalculateBonus(player, progress));
            progress.TotalCredits = 25;

            // MINE-004: Credits equal bonus percent (1:1 ratio)
            AssertEqual("MINE-004", "25 credits = 25% bonus", 25, AttributeModifierDefinitions.MiningSpeed.CalculateBonus(player, progress));

            // MINE-005: Float bonus calculation
            progress.TotalCredits = 25;

            // MINE-008: CalculateMaxCredits returns MaxMiningSpeedPercent
            AssertEqual("MINE-008", "CalculateMaxCredits matches MaxMiningSpeedPercent", maxMining, AttributeModifierDefinitions.MiningSpeed.GetMaxCredits(player));

            // MINE-009: Boundary - exactly at max
            progress.TotalCredits = maxMining;
            AssertEqual("MINE-009", "Exactly at max credits", maxMining, AttributeModifierDefinitions.MiningSpeed.CalculateBonus(player, progress));

            // MINE-010: Boundary - one over max
            progress.TotalCredits = maxMining+1;
            AssertEqual("MINE-010", "One over max credits still capped", maxMining, AttributeModifierDefinitions.MiningSpeed.CalculateBonus(player, progress));
        }

        // =========================================================================
        // MELEE TESTS
        // =========================================================================

        private static void RunMeleeTests()
        {
            // MELEE-001: First credit at base increment
            AssertEqual("MELEE-001", "Melee bonus percent at 1 credit", 1, SeraphLevelingModSystem.CalculateMeleeBonusPercent(1));

            // MELEE-002: Credits capped at max
            AssertEqual("MELEE-002", "Melee bonus capped at max (default 50)", SeraphLevelingModSystem.MaxMeleeDamagePercent, SeraphLevelingModSystem.CalculateMeleeBonusPercent(100));

            // MELEE-003: Zero credits yields zero bonus
            AssertEqual("MELEE-003", "Melee bonus at 0 credits", 0, SeraphLevelingModSystem.CalculateMeleeBonusPercent(0));

            // MELEE-004: Credits equal bonus percent (1:1 ratio)
            AssertEqual("MELEE-004", "25 credits = 25% bonus", 25, SeraphLevelingModSystem.CalculateMeleeBonusPercent(25));

            // MELEE-005: Max credits for null entity
            int maxCredits = SeraphLevelingModSystem.GetMaxMeleeCredits(null);
            AssertEqual("MELEE-005", "Max melee credits (null entity)", SeraphLevelingModSystem.MaxMeleeDamagePercent, maxCredits);

            // MELEE-006: Weapon detection - sword-copper
            string swordResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("game:sword-copper");
            AssertTrue("MELEE-006", "Sword detected as valid melee weapon", swordResult != null, "not null", swordResult ?? "null");

            // MELEE-007: Weapon detection - falx-copper
            string falxResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("game:falx-copper");
            AssertTrue("MELEE-007", "Falx detected as valid melee weapon", falxResult != null, "not null", falxResult ?? "null");

            // MELEE-008: Weapon detection - spear-copper
            string spearResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("game:spear-copper");
            AssertTrue("MELEE-008", "Spear detected as valid melee weapon", spearResult != null, "not null", spearResult ?? "null");

            // MELEE-009: Weapon detection - blade variant
            string bladeResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("blade-copper");
            AssertTrue("MELEE-009", "Blade detected as valid melee weapon", bladeResult != null, "not null", bladeResult ?? "null");

            // MELEE-010: Weapon detection - longsword variant
            string longswordResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("longsword-iron");
            AssertTrue("MELEE-010", "Longsword detected as valid melee weapon", longswordResult != null, "not null", longswordResult ?? "null");

            // MELEE-011: Weapon detection - shortsword variant
            string shortswordResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("shortsword-iron");
            AssertTrue("MELEE-011", "Shortsword detected as valid melee weapon", shortswordResult != null, "not null", shortswordResult ?? "null");

            // MELEE-012: Invalid weapon - knife
            string knifeResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("knife-copper");
            AssertTrue("MELEE-012", "Knife NOT detected as melee weapon", knifeResult == null, "null", knifeResult ?? "null");

            // MELEE-013: Invalid weapon - axe
            string axeResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("axe-copper");
            AssertTrue("MELEE-013", "Axe NOT detected as melee weapon", axeResult == null, "null", axeResult ?? "null");

            // MELEE-014: Invalid weapon - bow
            string bowResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("bow-long");
            AssertTrue("MELEE-014", "Bow NOT detected as melee weapon", bowResult == null, "null", bowResult ?? "null");

            // MELEE-015: Null input handling
            string nullResult = SeraphLevelingModSystem.GetWeaponTypeFromCode(null);
            AssertTrue("MELEE-015", "Null input returns null", nullResult == null, "null", nullResult ?? "null");

            // MELEE-016: Empty input handling
            string emptyResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("");
            AssertTrue("MELEE-016", "Empty input returns null", emptyResult == null, "null", emptyResult ?? "null");

            // MELEE-017: Full code preserved
            string fullCode = SeraphLevelingModSystem.GetWeaponTypeFromCode("game:sword-copper");
            AssertEqual("MELEE-017", "Full item code preserved", "game:sword-copper", fullCode);
        }

        // =========================================================================
        // ARMOR TESTS
        // =========================================================================

        private static void RunArmorTests()
        {
            // ARMOR-001: Armor durability bonus (null entity = no vanilla bonus)
            AssertEqual("ARMOR-001", "Armor durability at 25 credits", 25, SeraphLevelingModSystem.CalculateArmorDurabilityBonusPercent(25, null));

            // ARMOR-002: Armor walk speed bonus (null entity)
            AssertEqual("ARMOR-002", "Armor walk speed at 25 credits", 25, SeraphLevelingModSystem.CalculateArmorWalkSpeedBonusPercent(25, null));

            // ARMOR-003: Durability capped at max
            AssertEqual("ARMOR-003", "Armor durability capped", SeraphLevelingModSystem.MaxArmorDurabilityPercent, SeraphLevelingModSystem.CalculateArmorDurabilityBonusPercent(100, null));

            // ARMOR-004: Walk speed capped at max
            AssertEqual("ARMOR-004", "Armor walk speed capped", SeraphLevelingModSystem.MaxArmorWalkSpeedPercent, SeraphLevelingModSystem.CalculateArmorWalkSpeedBonusPercent(100, null));

            // ARMOR-005: Zero credits
            AssertEqual("ARMOR-005a", "Zero durability credits", 0, SeraphLevelingModSystem.CalculateArmorDurabilityBonusPercent(0, null));
            AssertEqual("ARMOR-005b", "Zero walk speed credits", 0, SeraphLevelingModSystem.CalculateArmorWalkSpeedBonusPercent(0, null));

            // ARMOR-006: Armor type detection - plate
            AssertEqual("ARMOR-006", "Plate armor detected", "plate", SeraphLevelingModSystem.GetArmorType("armor-body-plate-iron"));

            // ARMOR-007: Armor type detection - scale
            AssertEqual("ARMOR-007", "Scale armor detected", "scale", SeraphLevelingModSystem.GetArmorType("armor-body-scale-iron"));

            // ARMOR-008: Armor type detection - brigandine
            AssertEqual("ARMOR-008", "Brigandine armor detected", "brigandine", SeraphLevelingModSystem.GetArmorType("armor-body-brigandine-iron"));

            // ARMOR-009: Armor type detection - chain
            AssertEqual("ARMOR-009", "Chain armor detected", "chain", SeraphLevelingModSystem.GetArmorType("armor-body-chain-iron"));

            // ARMOR-010: Armor type detection - lamellar (treated as chain)
            AssertEqual("ARMOR-010", "Lamellar treated as chain", "chain", SeraphLevelingModSystem.GetArmorType("armor-body-lamellar-iron"));

            // ARMOR-011: Armor type detection - leather (light)
            AssertEqual("ARMOR-011", "Leather detected as light", "light", SeraphLevelingModSystem.GetArmorType("armor-body-leather"));

            // ARMOR-012: Armor type detection - gambeson (light)
            AssertEqual("ARMOR-012", "Gambeson detected as light", "light", SeraphLevelingModSystem.GetArmorType("armor-body-gambeson"));

            // ARMOR-013: Non-armor returns null
            string nonArmor = SeraphLevelingModSystem.GetArmorType("clothes-upperbody-shirt");
            AssertTrue("ARMOR-013", "Non-armor returns null", nonArmor == null, "null", nonArmor ?? "null");

            // ARMOR-014: Null input
            string nullResult = SeraphLevelingModSystem.GetArmorType(null);
            AssertTrue("ARMOR-014", "Null input returns null", nullResult == null, "null", nullResult ?? "null");

            // ARMOR-015: First equip bonus - plate
            AssertEqual("ARMOR-015", "Plate first equip bonus", SeraphLevelingModSystem.FirstEquipPlateBonus, SeraphLevelingModSystem.GetFirstEquipBonus("plate"));

            // ARMOR-016: First equip bonus - scale
            AssertEqual("ARMOR-016", "Scale first equip bonus", SeraphLevelingModSystem.FirstEquipScaleBonus, SeraphLevelingModSystem.GetFirstEquipBonus("scale"));

            // ARMOR-017: First equip bonus - brigandine
            AssertEqual("ARMOR-017", "Brigandine first equip bonus", SeraphLevelingModSystem.FirstEquipBrigandineBonus, SeraphLevelingModSystem.GetFirstEquipBonus("brigandine"));

            // ARMOR-018: First equip bonus - chain
            AssertEqual("ARMOR-018", "Chain first equip bonus", SeraphLevelingModSystem.FirstEquipChainBonus, SeraphLevelingModSystem.GetFirstEquipBonus("chain"));

            // ARMOR-019: First equip bonus - light (default)
            AssertEqual("ARMOR-019", "Light first equip bonus", SeraphLevelingModSystem.FirstEquipLightBonus, SeraphLevelingModSystem.GetFirstEquipBonus("light"));

            // ARMOR-020: Walk speed first equip bonus - plate
            AssertEqual("ARMOR-020", "Plate walk speed bonus", SeraphLevelingModSystem.FirstEquipWalkSpeedPlateBonus, SeraphLevelingModSystem.GetFirstEquipWalkSpeedBonus("plate"));

            // ARMOR-021: Full armor code with game: prefix
            AssertEqual("ARMOR-021", "Game prefix handled", "plate", SeraphLevelingModSystem.GetArmorType("game:armor-body-plate-iron"));
        }

        // =========================================================================
        // NEGATIVE TRAIT CANCELLATION TESTS
        // =========================================================================

        private static void RunNegativeTraitTests()
        {
            // NEG-001: CalculateRemainingPenalty - basic
            AssertEqual("NEG-001", "Remaining penalty 15-10=5", 5, SeraphLevelingModSystem.CalculateRemainingPenalty(15, 10));

            // NEG-002: CalculateRemainingPenalty - fully cancelled
            AssertEqual("NEG-002", "Remaining penalty 15-15=0", 0, SeraphLevelingModSystem.CalculateRemainingPenalty(15, 15));

            // NEG-003: CalculateRemainingPenalty - over-cancelled stays at 0
            AssertEqual("NEG-003", "Remaining penalty 15-20=0 (not negative)", 0, SeraphLevelingModSystem.CalculateRemainingPenalty(15, 20));

            // NEG-004: CalculateRemainingPenalty - zero progress
            AssertEqual("NEG-004", "Remaining penalty 15-0=15", 15, SeraphLevelingModSystem.CalculateRemainingPenalty(15, 0));

            // NEG-005: CalculateRemainingPenalty - negative bonus increases penalty (math: 15-(-5)=20)
            AssertEqual("NEG-005", "Remaining penalty 15-(-5)=20", 20, SeraphLevelingModSystem.CalculateRemainingPenalty(15, -5));

            // NEG-006: Farsighted penalty constant
            AssertEqual("NEG-006", "Farsighted penalty is 15", 15, SeraphLevelingModSystem.VANILLA_FARSIGHTED_MELEE_PENALTY);

            // NEG-007: Nervous penalty constant
            AssertEqual("NEG-007", "Nervous penalty is 15", 15, SeraphLevelingModSystem.VANILLA_NERVOUS_MELEE_PENALTY);

            // NEG-008: Nearsighted penalty constant
            AssertEqual("NEG-008", "Nearsighted penalty is 15", 15, SeraphLevelingModSystem.VANILLA_NEARSIGHTED_RANGED_PENALTY);

            // NEG-009: Frail distance penalty constant
            AssertEqual("NEG-009", "Frail distance penalty is 25", 25, SeraphLevelingModSystem.VANILLA_FRAIL_DISTANCE_PENALTY);

            // NEG-010: Civil foraging penalty constant
            AssertEqual("NEG-010", "Civil foraging penalty is 10", 10, SeraphLevelingModSystem.VANILLA_CIVIL_FORAGING_PENALTY);

            // NEG-011: Weak mining penalty constant
            AssertEqual("NEG-011", "Weak mining penalty is 10", 10, SeraphLevelingModSystem.VANILLA_WEAK_MINING_PENALTY);

            // NEG-012: Claustrophobic mining penalty constant
            AssertEqual("NEG-012", "Claustrophobic mining penalty is 10", 10, SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_MINING_PENALTY);

            // NEG-013: Ravenous hunger penalty constant
            AssertEqual("NEG-013", "Ravenous hunger penalty is 30", 30, SeraphLevelingModSystem.VANILLA_RAVENOUS_HUNGER_PENALTY);

            // NEG-014: Kind loot penalty constant
            AssertEqual("NEG-014", "Kind loot penalty is 10", 10, SeraphLevelingModSystem.VANILLA_KIND_LOOT_PENALTY);

            // NEG-015: Kind speed penalty constant
            AssertEqual("NEG-015", "Kind speed penalty is 25", 25, SeraphLevelingModSystem.VANILLA_KIND_SPEED_PENALTY);

            // NEG-016: Heavyhanded vessel penalty constant
            AssertEqual("NEG-016", "Heavyhanded vessel penalty is 10", 10, SeraphLevelingModSystem.VANILLA_HEAVYHANDED_VESSEL_PENALTY);

            // NEG-017: Heavyhanded foraging penalty constant
            AssertEqual("NEG-017", "Heavyhanded foraging penalty is 15", 15, SeraphLevelingModSystem.VANILLA_HEAVYHANDED_FORAGING_PENALTY);

            // NEG-018: Heavyhanded wild crop penalty constant
            AssertEqual("NEG-018", "Heavyhanded wild crop penalty is 20", 20, SeraphLevelingModSystem.VANILLA_HEAVYHANDED_WILD_CROP_PENALTY);

            // NEG-019: Claustrophobic ore penalty constant
            AssertEqual("NEG-019", "Claustrophobic ore penalty is 15", 15, SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_ORE_PENALTY);

            // NEG-020: Partial cancellation simulation - Farsighted at level 10
            int farsightedRemaining = SeraphLevelingModSystem.CalculateRemainingPenalty(SeraphLevelingModSystem.VANILLA_FARSIGHTED_MELEE_PENALTY, 10);
            AssertEqual("NEG-020", "Farsighted at level 10 = 5% remaining", 5, farsightedRemaining);

            // NEG-021: Full cancellation simulation - Nervous at level 15
            int nervousRemaining = SeraphLevelingModSystem.CalculateRemainingPenalty(SeraphLevelingModSystem.VANILLA_NERVOUS_MELEE_PENALTY, 15);
            AssertEqual("NEG-021", "Nervous at level 15 = 0% remaining", 0, nervousRemaining);
        }

        // =========================================================================
        // DETECTION TESTS
        // =========================================================================

        private static void RunDetectionTests()
        {
            // DET-001: Clothing detection - clothes prefix
            AssertTrue("DET-001", "clothes- detected as clothing", IsClothingItemPublic("clothes-upperbody-shirt-linen"), "true", "false");

            // DET-002: Clothing detection - shirt prefix
            AssertTrue("DET-002", "shirt- detected as clothing", IsClothingItemPublic("shirt-linen"), "true", "false");

            // DET-003: Clothing detection - trousers prefix
            AssertTrue("DET-003", "trousers- detected as clothing", IsClothingItemPublic("trousers-linen"), "true", "false");

            // DET-004: Clothing detection - dress prefix
            AssertTrue("DET-004", "dress- detected as clothing", IsClothingItemPublic("dress-wool"), "true", "false");

            // DET-005: Clothing detection - hat prefix
            AssertTrue("DET-005", "hat- detected as clothing", IsClothingItemPublic("hat-straw"), "true", "false");

            // DET-006: Clothing detection - cape prefix
            AssertTrue("DET-006", "cape- detected as clothing", IsClothingItemPublic("cape-wool"), "true", "false");

            // DET-007: Clothing detection - boots prefix
            AssertTrue("DET-007", "boots- detected as clothing", IsClothingItemPublic("boots-leather"), "true", "false");

            // DET-008: Armor NOT detected as clothing
            AssertTrue("DET-008", "armor NOT detected as clothing", !IsClothingItemPublic("armor-body-plate-iron"), "false", "true");

            // DET-009: Armor detection
            AssertTrue("DET-009", "armor- detected as armor", IsArmorItemPublic("armor-body-plate-iron"), "true", "false");

            // DET-010: Clothing NOT detected as armor
            AssertTrue("DET-010", "clothes NOT detected as armor", !IsArmorItemPublic("clothes-upperbody-shirt"), "false", "true");

            // DET-011: Null handling - clothing
            AssertTrue("DET-011", "Null not detected as clothing", !IsClothingItemPublic(null), "false", "true");

            // DET-012: Null handling - armor
            AssertTrue("DET-012", "Null not detected as armor", !IsArmorItemPublic(null), "false", "true");

            // DET-013: Empty handling - clothing
            AssertTrue("DET-013", "Empty not detected as clothing", !IsClothingItemPublic(""), "false", "true");

            // DET-014: Empty handling - armor
            AssertTrue("DET-014", "Empty not detected as armor", !IsArmorItemPublic(""), "false", "true");

            // DET-015: Case insensitivity - clothing
            AssertTrue("DET-015", "CLOTHES- detected (case insensitive)", IsClothingItemPublic("CLOTHES-upperbody-shirt"), "true", "false");

            // DET-016: Case insensitivity - armor
            AssertTrue("DET-016", "ARMOR- detected (case insensitive)", IsArmorItemPublic("ARMOR-body-plate-iron"), "true", "false");
        }

        // =========================================================================
        // PERSISTENCE TESTS
        // =========================================================================

        private static void RunPersistenceTests(IServerPlayer player)
        {
            if (player?.Entity == null)
            {
                results.Add(new TestResult
                {
                    TestId = "PERS-000",
                    Description = "Player entity available",
                    Passed = false,
                    ExpectedValue = "not null",
                    ActualValue = "null"
                });
                return;
            }

            string playerUid = player.PlayerUID;
            var watchedAttrs = player.Entity.WatchedAttributes;

            // PERS-001: Mining data exists in dictionary
            bool hasMiningData = AttributeModifierDefinitions.MiningSpeed.ProgressDictionary.ContainsKey(playerUid);
            AssertTrue("PERS-001", "Mining data exists in dictionary", hasMiningData, "exists", "missing");

            // PERS-002: Mining WatchedAttributes matches dictionary
            if (hasMiningData)
            {
                var miningData = AttributeModifierDefinitions.MiningSpeed.ProgressDictionary[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_MINING_LEVEL, -999);
                AssertEqual("PERS-002", "Mining level synced to WatchedAttributes", miningData.TotalCredits, watchedLevel);
            }

            // PERS-003: Melee data exists in dictionary
            bool hasMeleeData = SeraphLevelingModSystem.MeleeProgress.ContainsKey(playerUid);
            AssertTrue("PERS-003", "Melee data exists in dictionary", hasMeleeData, "exists", "missing");

            // PERS-004: Melee WatchedAttributes matches dictionary
            if (hasMeleeData)
            {
                var meleeData = SeraphLevelingModSystem.MeleeProgress[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_LEVEL, -999);
                AssertEqual("PERS-004", "Melee level synced to WatchedAttributes", meleeData.TotalCredits, watchedLevel);
            }

            // PERS-005: Ranged data exists in dictionary
            bool hasRangedData = AttributeModifierDefinitions.RangedDamage.ProgressDictionary.ContainsKey(playerUid);
            AssertTrue("PERS-005", "Ranged data exists in dictionary", hasRangedData, "exists", "missing");

            // PERS-006: Ranged WatchedAttributes matches dictionary
            if (hasRangedData)
            {
                var rangedData = AttributeModifierDefinitions.RangedDamage.ProgressDictionary[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_LEVEL, -999);
                AssertEqual("PERS-006", "Ranged level synced to WatchedAttributes", rangedData.TotalCredits, watchedLevel);
            }

            // PERS-007: Walking data exists in dictionary
            bool hasWalkingData = AttributeModifierDefinitions.WalkingSpeed.ProgressDictionary.ContainsKey(playerUid);
            AssertTrue("PERS-007", "Walking data exists in dictionary", hasWalkingData, "exists", "missing");

            // PERS-008: Walking WatchedAttributes matches dictionary
            if (hasWalkingData)
            {
                var walkingData = AttributeModifierDefinitions.WalkingSpeed.ProgressDictionary[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, -999);
                AssertEqual("PERS-008", "Walking level synced to WatchedAttributes", walkingData.TotalCredits, watchedLevel);
            }

            // PERS-009: Hunger data exists in dictionary
            bool hasHungerData = AttributeModifierDefinitions.HungerRate.ProgressDictionary.ContainsKey(playerUid);
            AssertTrue("PERS-009", "Hunger data exists in dictionary", hasHungerData, "exists", "missing");

            // PERS-010: Hunger WatchedAttributes matches dictionary
            if (hasHungerData)
            {
                var hungerData = AttributeModifierDefinitions.HungerRate.ProgressDictionary[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_HUNGER_LEVEL, -999);
                AssertEqual("PERS-010", "Hunger level synced to WatchedAttributes", hungerData.TotalCredits, watchedLevel);
            }

            // PERS-011: Armor data exists in dictionary
            bool hasArmorData = SeraphLevelingModSystem.ArmorProgress.ContainsKey(playerUid);
            AssertTrue("PERS-011", "Armor data exists in dictionary", hasArmorData, "exists", "missing");

            // PERS-012: Armor durability WatchedAttributes matches dictionary
            if (hasArmorData)
            {
                var armorData = SeraphLevelingModSystem.ArmorProgress[playerUid];
                int watchedDurability = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_DURABILITY_LEVEL, -999);
                AssertEqual("PERS-012", "Armor durability synced to WatchedAttributes", armorData.TotalDurabilityCredits, watchedDurability);

                int watchedWalkSpeed = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_WALKSPEED_LEVEL, -999);
                AssertEqual("PERS-013", "Armor walk speed synced to WatchedAttributes", armorData.TotalWalkSpeedCredits, watchedWalkSpeed);
            }

            // PERS-014: Mining data structure integrity
            if (hasMiningData)
            {
                var miningData = AttributeModifierDefinitions.MiningSpeed.ProgressDictionary[playerUid];
                bool creditsValid = miningData.TotalCredits >= 0;
                bool pickaxeProgressValid = miningData.ToolProgress != null;
                AssertTrue("PERS-014", "Mining data structure valid", creditsValid && pickaxeProgressValid, "valid", "corrupted");
            }

            // PERS-015: Melee data structure integrity
            if (hasMeleeData)
            {
                var meleeData = SeraphLevelingModSystem.MeleeProgress[playerUid];
                bool creditsValid = meleeData.TotalCredits >= 0;
                bool weaponProgressValid = meleeData.WeaponProgress != null;
                AssertTrue("PERS-015", "Melee data structure valid", creditsValid && weaponProgressValid, "valid", "corrupted");
            }

            // PERS-016: Ranged data structure integrity
            if (hasRangedData)
            {
                var rangedData = AttributeModifierDefinitions.RangedDamage.ProgressDictionary[playerUid];
                bool creditsValid = rangedData.TotalCredits >= 0;
                bool weaponProgressValid = rangedData.ToolProgress != null;
                AssertTrue("PERS-016", "Ranged data structure valid", creditsValid && weaponProgressValid, "valid", "corrupted");
            }

            // PERS-017: Armor data structure integrity
            if (hasArmorData)
            {
                var armorData = SeraphLevelingModSystem.ArmorProgress[playerUid];
                bool durabilityValid = armorData.TotalDurabilityCredits >= 0;
                bool walkSpeedValid = armorData.TotalWalkSpeedCredits >= 0;
                bool armorPiecesValid = armorData.ArmorProgress != null;
                AssertTrue("PERS-017", "Armor data structure valid", durabilityValid && walkSpeedValid && armorPiecesValid, "valid", "corrupted");
            }
        }

        // Helper methods that mirror the private methods in the mod
        private static bool IsClothingItemPublic(string itemCode)
        {
            return SeraphLevelingModSystem.IsClothingItem(itemCode);
        }

        private static bool IsArmorItemPublic(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();
            return lowerCode.Contains("armor-");
        }
    }
}
