using System;
using SeraphLeveling.Data.Attributes;
using SeraphLeveling.Data.Traits;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SeraphLeveling.Patches
{
    /// <summary>
    /// Harmony patch methods for CharacterSystem.
    /// </summary>
    public static class CharacterSystemPatches
    {
        // Reference to the client API, set during patch application
        public static ICoreClientAPI ClientApi { get; set; }

        // The actual vanilla lang key is the literal phrase "No positive or negative traits"
        // (yes, the key contains spaces). The previous "charactersheet-notraits" key doesn't
        // exist in vanilla, so Lang.Get returned the key string itself, defeating the
        // localization check.
        public const string NO_TRAITS_KEY = "No positive or negative traits";

        public const string FULL_TRAIT_MESSAGE_KEY = "traitwithattributes";

        /// <summary>
        /// Postfix for getClassTraitText - adds dynamic mining and melee progression info.
        /// The method has NO parameters - it's an instance method on CharacterSystem.
        /// </summary>
        public static void GetClassTraitText_Postfix(ref string __result)
        {
            // Get the player entity from the client API
            if (ClientApi == null) return;

            EntityPlayer eplr = ClientApi.World?.Player?.Entity;
            if (eplr == null) return;

            // Log the raw result string to see exact format (escape special chars for visibility)
            string escapedResult = __result?.Replace("\n", "\\n").Replace("\r", "\\r") ?? "NULL";
            ClientApi.Logger.Debug($"[SeraphLeveling] RAW getClassTraitText result: {escapedResult}");

            // Get mining progression data
            int miningLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MINING_LEVEL, 0);
            int miningBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MINING_BONUS, 0);
            bool hasVanillaHardy = SeraphLevelingModSystem.PlayerHasTrait(eplr, TraitDefinitions.Hardy);

            // Get melee progression data
            int meleeLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_LEVEL, 0);
            int meleeBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_BONUS, 0);
            bool hasVanillaSoldier = SeraphLevelingModSystem.PlayerHasTrait(eplr, TraitDefinitions.Soldier);

            // Get ranged progression data
            int rangedLevel = eplr.WatchedAttributes.GetInt(AttributeModifierDefinitions.RangedDamage.WatchedLevel, 0);
            int rangedDamageBonus = eplr.WatchedAttributes.GetInt(AttributeModifierDefinitions.RangedDamage.WatchedBonus, 0);
            int rangedAccuracyBonus = eplr.WatchedAttributes.GetInt(AttributeModifierDefinitions.RangedAccuracy.WatchedBonus, 0);
            int rangedDistanceBonus = eplr.WatchedAttributes.GetInt(AttributeModifierDefinitions.RangedDistance.WatchedBonus, 0);
            bool hasVanillaFocused = SeraphLevelingModSystem.PlayerHasTrait(eplr, TraitDefinitions.Focused);

            // Get walking progression data
            int walkingLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, 0);
            int walkingBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_BONUS, 0);
            bool hasVanillaFleetfooted = SeraphLevelingModSystem.PlayerHasTrait(eplr, TraitDefinitions.Fleetfooted);

            // Get armor progression data
            int armorDurabilityLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_DURABILITY_LEVEL, 0);
            int armorDurabilityBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_DURABILITY_BONUS, 0);
            int armorWalkSpeedLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_WALKSPEED_LEVEL, 0);
            int armorWalkSpeedBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_WALKSPEED_BONUS, 0);
            bool hasVanillaSoldierArmor = SeraphLevelingModSystem.PlayerHasTrait(eplr, TraitDefinitions.Soldier);

            ClientApi.Logger.Debug($"[SeraphLeveling] getClassTraitText postfix called. Mining: Level={miningLevel}, Bonus={miningBonus}%, HasHardy={hasVanillaHardy} | Melee: Level={meleeLevel}, Bonus={meleeBonus}%, HasSoldier={hasVanillaSoldier} | Ranged: Level={rangedLevel}, HasFocused={hasVanillaFocused} | Walking: Level={walkingLevel}, HasFleetfooted={hasVanillaFleetfooted} | Armor: Dur={armorDurabilityLevel}, Walk={armorWalkSpeedLevel}");

            ClientApi.Logger.Debug($"[SeraphLeveling] Original result: '{__result}', noTraitsMsg: '{Lang.Get(NO_TRAITS_KEY)}'");

            // Check if we have NO real traits (only "no traits" message or empty)
            // Use Contains to handle cases where the message might have formatting
            bool hasNoTraits = HasNoTraits(__result);

            // Process loaded traits
            foreach (var trait in SeraphLevelingModSystem.LoadedTraits)
            {
                trait.BuildTraitText(eplr, ref __result);
            }

            // =========================================================================
            // COMBAT OVERHAUL PROFICIENCY TRAIT DISPLAY
            // Display CO proficiencies that have credits > 0
            // =========================================================================

            // Defense-in-depth: even if a save has stale CO watched attrs from a previous
            // installation, never render CO trait lines when CO itself isn't installed.
            // The Apply/cache layer should already keep these attrs at 0/false when CO is
            // unloaded (via the IsCombatOverhaulLoaded gate in PopulateVanillaTraitsCache),
            // but skipping the entire section also avoids any future code path leaking a
            // phantom CO debuff through.
            if (SeraphLevelingModSystem.IsCombatOverhaulLoaded)
            {

            // Check if CO is enabled by looking for any CO credits
            var coProficiencies = new (string statName, string displayName, float maxBonus)[]
            {
                ("bowsProficiency", "Bows Proficiency", 0.5f),
                ("crossbowsProficiency", "Crossbows Proficiency", 0.5f),
                ("firearmsProficiency", "Firearms Proficiency", 0.5f),
                ("slingsProficiency", "Slings Proficiency", 0.3f),
                ("oneHandedSwordsProficiency", "One-Handed Swords", 0.3f),
                ("twoHandedSwordsProficiency", "Two-Handed Swords", 0.3f),
                ("spearsProficiency", "Spears Proficiency", 0.3f),
                ("javelinsProficiency", "Javelins Proficiency", 0.3f),
                ("macesProficiency", "Maces Proficiency", 0.3f),
                ("clubsProficiency", "Clubs Proficiency", 0.3f),
                ("halberdsProficiency", "Halberds Proficiency", 0.3f),
                ("poleaxeProficiency", "Poleaxe Proficiency", 0.3f),
                ("axesProficiency", "Axes Proficiency", 0.3f),
                ("quarterstaffProficiency", "Quarterstaff Proficiency", 0.3f),
            };

            foreach (var (statName, displayName, maxBonus) in coProficiencies)
            {
                string watchedKey = $"sitCO{statName}Credits";
                int credits = eplr.WatchedAttributes.GetInt(watchedKey, 0);
                if (credits > 0)
                {
                    float bonus = credits * 0.01f;
                    if (bonus > maxBonus) bonus = maxBonus;
                    string coTrait = $"<font color=\"#84ff84\">• {displayName} </font> <font opacity=\"0.6\">(+{bonus * 100:F0}% attack speed)</font>";

                    // Re-check hasNoTraits
                    hasNoTraits = HasNoTraits(__result);

                    if (hasNoTraits)
                    {
                        __result = coTrait;
                    }
                    else
                    {
                        __result = __result + "\n" + coTrait;
                    }
                }
            }

            // Steady Aim and Trembling Aim display
            int steadyAimCredits = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CO_STEADY_AIM_CREDITS, 0);
            float tremblingAimRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_CO_TREMBLING_AIM_REMAINING, 0f);
            bool hasTremblingAim = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_CO_HAS_TREMBLING_AIM, false);

            // Calculate net Steady Aim bonus (after cancellation if applicable)
            float steadyAimNetBonus = 0f;
            if (hasTremblingAim)
            {
                // For players with Trembling Aim: first 30 credits cancel the penalty, rest is bonus
                int creditsForBonus = Math.Max(0, steadyAimCredits - 30);
                steadyAimNetBonus = creditsForBonus * 0.01f;
            }
            else
            {
                steadyAimNetBonus = steadyAimCredits * 0.01f;
            }
            if (steadyAimNetBonus > 0.5f) steadyAimNetBonus = 0.5f;

            // Show Steady Aim positive trait only if there's a net bonus
            if (steadyAimNetBonus > 0)
            {
                string steadyAimTrait = $"<font color=\"#84ff84\">• Steady Aim </font> <font opacity=\"0.6\">(+{steadyAimNetBonus * 100:F0}% aim stability)</font>";

                hasNoTraits = HasNoTraits(__result);

                if (hasNoTraits)
                {
                    __result = steadyAimTrait;
                }
                else
                {
                    __result = __result + "\n" + steadyAimTrait;
                }
            }

            // Get other CO negative trait remaining values
            float clumsyHandsRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_CO_CLUMSY_HANDS_REMAINING, 0f);
            float weakHandRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_CO_WEAK_HAND_REMAINING, 0f);
            int fearOfMeleeRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CO_FEAR_OF_MELEE_REMAINING, 0);
            int coNervousRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CO_NERVOUS_REMAINING, 0);

            // Show Trembling Aim negative trait only if there's remaining penalty
            if (tremblingAimRemaining > 0)
            {
                string tremblingTrait = $"<font color=\"#ff8484\">• Trembling Aim </font> <font opacity=\"0.6\">(-{tremblingAimRemaining * 100:F0}% steady aim)</font>";
                __result = __result + "\n" + tremblingTrait;
            }

            // Show Clumsy Hands negative trait only if there's remaining penalty
            if (clumsyHandsRemaining > 0)
            {
                string clumsyTrait = $"<font color=\"#ff8484\">• Clumsy Hands </font> <font opacity=\"0.6\">(-{clumsyHandsRemaining * 100:F0}% ranged proficiency)</font>";
                __result = __result + "\n" + clumsyTrait;
            }

            // Show Weak Hand negative trait only if there's remaining penalty
            if (weakHandRemaining > 0)
            {
                string weakHandTrait = $"<font color=\"#ff8484\">• Weak Hand </font> <font opacity=\"0.6\">(-{weakHandRemaining * 100:F0}% ranged proficiency)</font>";
                __result = __result + "\n" + weakHandTrait;
            }

            // Show Fear of Melee negative trait only if there's remaining penalty
            if (fearOfMeleeRemaining > 0)
            {
                string fearTrait = $"<font color=\"#ff8484\">• Fear of Melee </font> <font opacity=\"0.6\">(-{fearOfMeleeRemaining} melee tier)</font>";
                __result = __result + "\n" + fearTrait;
            }

            // Show CO Nervous negative trait only if there's remaining penalty (piercing melee)
            if (coNervousRemaining > 0)
            {
                string coNervousTrait = $"<font color=\"#ff8484\">• Nervous </font> <font opacity=\"0.6\">(-{coNervousRemaining} piercing tier)</font>";
                __result = __result + "\n" + coNervousTrait;
            }

            } // end of IsCombatOverhaulLoaded gate around CO trait display

            // Clean up any newline issues that might have been introduced
            // First normalize line endings (handle \r\n, \r, and \n)
            __result = __result.Replace("\r\n", "\n").Replace("\r", "\n");

            // Remove any lines that are empty or whitespace-only
            // Also filter out CO's native negative trait displays when we're handling them
            var lines = __result.Split('\n');
            var nonEmptyLines = new System.Collections.Generic.List<string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string trimmedLine = line.Trim();
                string lowerLine = trimmedLine.ToLowerInvariant();

                // Filter out CO's native negative trait displays - we handle them ourselves
                // Check for various CO trait name patterns (case-insensitive)
                bool isCONativeTrait = false;

                // Trembling Aim - CO displays as "Trembling Aim (30% more aim drift)" or similar
                if (lowerLine.Contains("trembling") && (lowerLine.Contains("aim") || lowerLine.Contains("drift")))
                {
                    // Only filter if it's NOT our display (our display has "steady aim" in it)
                    if (!lowerLine.Contains("steady aim"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Clumsy Hands - CO displays as "Clumsy Hands" with some description
                if (lowerLine.Contains("clumsy") && lowerLine.Contains("hands"))
                {
                    // Only filter if it's NOT our display (our display has "ranged proficiency" in it)
                    if (!lowerLine.Contains("ranged proficiency"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Weak Hand - CO displays as "Weak Hand" with some description
                if (lowerLine.Contains("weak") && lowerLine.Contains("hand"))
                {
                    // Only filter if it's NOT our display (our display has "ranged proficiency" in it)
                    if (!lowerLine.Contains("ranged proficiency"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Fear of Melee - CO displays as "Fear of Melee" with description
                if (lowerLine.Contains("fear") && lowerLine.Contains("melee"))
                {
                    // Only filter if it's NOT our display (our display has "melee tier" in it)
                    if (!lowerLine.Contains("melee tier"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Nervous - CO displays as "Nervous" with description about piercing damage
                if (lowerLine.Contains("nervous") && lowerLine.Contains("piercing") && lowerLine.Contains("damage"))
                {
                    // Only filter if it's NOT our display (our display has "piercing tier" in it)
                    if (!lowerLine.Contains("piercing tier"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Filter out CO's native positive proficiency trait displays
                // CO displays proficiencies like "Halberds Proficiency (+30% attack speed)"
                // We replace these with our own dynamic version that shows current progress
                // Only filter if this is NOT our display (our display has green font markup #84ff84)
                if (!lowerLine.Contains("#84ff84"))
                {
                    // Check for proficiency names in the line with a percentage bonus
                    string[] proficiencyNames = new[] {
                        "bows proficiency", "crossbows proficiency", "firearms proficiency", "slings proficiency",
                        "one-handed swords", "two-handed swords", "spears proficiency", "javelins proficiency",
                        "maces proficiency", "clubs proficiency", "halberds proficiency", "poleaxe proficiency", "axes proficiency",
                        "quarterstaff proficiency"
                    };

                    foreach (var profName in proficiencyNames)
                    {
                        // Filter if line has proficiency name AND a percentage (CO's format)
                        // Match patterns like "(+30%", "(+30% attack speed)", etc.
                        if (lowerLine.Contains(profName) && (lowerLine.Contains("attack speed") ||
                            System.Text.RegularExpressions.Regex.IsMatch(lowerLine, @"\(\+\d+%")))
                        {
                            isCONativeTrait = true;
                            break;
                        }
                    }

                    // Steady Aim is separate - filter if it has a percentage but not our markup
                    // CO might display "Steady Aim (+50% reduced aim drift)" or similar
                    if (lowerLine.Contains("steady aim") &&
                        System.Text.RegularExpressions.Regex.IsMatch(lowerLine, @"\(\+\d+%"))
                    {
                        isCONativeTrait = true;
                    }
                }

                if (isCONativeTrait)
                {
                    continue; // Skip CO's native display
                }

                nonEmptyLines.Add(trimmedLine);
            }
            __result = string.Join("\n", nonEmptyLines);

            // Defensive cleanup: collapse any duplicate-bullet patterns introduced
            // by overlapping regex replacements on vanilla trait text. Catches things
            // like "• • Claustrophobic" or nested-font variants regardless of which
            // earlier replacement step produced them.
            __result = CollapseDuplicateBullets(__result);

            // Final trim
            __result = __result.Trim();

            ClientApi.Logger.Debug($"[SeraphLeveling] Modified result: {__result}");
        }

        private static readonly System.Text.RegularExpressions.Regex DuplicateBulletPair =
            new System.Text.RegularExpressions.Regex(
                @"•(?<between>(?:[ \t]|<font[^>]*>|</font>)*)•",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Cache compiled "orphan-only" regexes per plain trait name (e.g. "<font color="#84ff84">• Hardy </font>").
        // These match the plain name ONLY when it's a standalone line entry (followed by newline or
        // end-of-string), not when it's the leading name of a vanilla trait line that has its own
        // " <font opacity..."  description tag immediately after. That distinction is critical:
        // unrestricted Contains/Replace on the plain name corrupts vanilla lines (strips the name
        // or inserts our dynamic before the vanilla description, leaving both descriptions stacked).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex> OrphanTraitPatternCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex>();

        private static System.Text.RegularExpressions.Regex GetOrphanTraitPattern(string plainName)
        {
            return OrphanTraitPatternCache.GetOrAdd(plainName, key =>
                new System.Text.RegularExpressions.Regex(
                    @"\n?" + System.Text.RegularExpressions.Regex.Escape(key) + @"(?=\n|$)",
                    System.Text.RegularExpressions.RegexOptions.Compiled));
        }

        /// <summary>
        /// Returns true if plainName appears as a standalone entry in text
        /// (followed by a newline or end-of-string), not as a substring of a longer vanilla line.
        /// </summary>
        public static bool ContainsOrphanTraitName(string text, string plainName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(plainName)) return false;
            return GetOrphanTraitPattern(plainName).IsMatch(text);
        }

        /// <summary>
        /// Replaces standalone occurrences of plainName with the given replacement.
        /// Preserves the leading newline if matched. Vanilla lines are left untouched.
        /// </summary>
        public static string ReplaceOrphanTraitName(string text, string plainName, string replacement)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(plainName)) return text;
            return GetOrphanTraitPattern(plainName).Replace(text, m =>
                m.Value.StartsWith("\n") ? "\n" + replacement : replacement);
        }

        /// <summary>
        /// Removes standalone occurrences of plainName (and the preceding newline if present).
        /// Vanilla lines are left untouched.
        /// </summary>
        public static string RemoveOrphanTraitName(string text, string plainName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(plainName)) return text;
            return GetOrphanTraitPattern(plainName).Replace(text, "");
        }

        // Cache for locale-aware vanilla trait line regexes. Built lazily per trait code from
        // Lang.Get("trait-{code}"), so the regex works against whatever language the client is
        // running. Without this, hardcoded English wording (Claustrophobic, "ore drop rate", etc.)
        // would fail to match a French/German/etc. client and the vanilla line would render with
        // its full original penalty wording even after the mod has functionally cancelled it.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex> VanillaTraitLineRegexCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex>();

        /// <summary>
        /// Builds (or returns a cached) regex matching a full vanilla trait line in any locale.
        /// Anchors on Lang.Get("trait-{code}") (which already includes the colored font + bullet)
        /// and consumes the trailing "(...)" opacity font block. Includes an optional leading
        /// newline so the same regex can be used for both replacement and removal.
        /// Returns null if the trait lang key is unavailable.
        /// </summary>
        private static System.Text.RegularExpressions.Regex GetVanillaTraitLineRegex(string traitCode)
        {
            return VanillaTraitLineRegexCache.GetOrAdd(traitCode, code =>
            {
                string traitText = Lang.Get("trait-" + code);
                if (string.IsNullOrEmpty(traitText) || traitText == "trait-" + code)
                {
                    return null;
                }
                string escaped = System.Text.RegularExpressions.Regex.Escape(traitText);
                // Pattern: <optional newline><localized trait label><whitespace><opacity font>(<any non-tag content>)</font>
                // We don't constrain the description content because it's a localized list of
                // charattribute strings joined with ", " — different in every language.
                string pattern = @"\n?" + escaped + @"\s*<font[^>]*opacity[^>]*>[^<]*</font>";
                return new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Compiled);
            });
        }

        /// <summary>
        /// Replaces the entire vanilla trait line with the given replacement text. Preserves the
        /// leading newline if matched (so adjacent trait entries stay separated). No-op if the
        /// line isn't found.
        /// </summary>
        public static string ReplaceVanillaTraitLine(string text, string traitCode, string replacement)
        {
            var regex = GetVanillaTraitLineRegex(traitCode);
            if (regex == null) return text;
            return regex.Replace(text, m => m.Value.StartsWith("\n") ? "\n" + replacement : replacement);
        }

        /// <summary>
        /// Removes the entire vanilla trait line, including the leading newline if matched.
        /// No-op if the line isn't found.
        /// </summary>
        public static string RemoveVanillaTraitLine(string text, string traitCode)
        {
            var regex = GetVanillaTraitLineRegex(traitCode);
            if (regex == null) return text;
            return regex.Replace(text, "");
        }

        /// <summary>
        /// Replaces a single inline vanilla charattribute string (e.g., "+10% mining speed") with
        /// a new value while keeping the same locale. Looks up the localized base string via
        /// Lang.Get("charattribute-{statKey}-{baseValue}"), then substitutes the first integer in
        /// that string with newPercent. Used when a class already has a vanilla positive trait
        /// (e.g., Blackguard's Hardy +10% mining speed) and we want to combine it with our
        /// progression bonus inline rather than replacing the whole line.
        /// </summary>
        public static string ReplaceVanillaCharAttribute(string text, string statKey, double baseValue, int newPercent)
        {
            string baseLangKey = "charattribute-" + statKey + "-" +
                baseValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string baseLocalized = Lang.Get(baseLangKey);
            if (string.IsNullOrEmpty(baseLocalized) || baseLocalized == baseLangKey || !text.Contains(baseLocalized))
            {
                return text;
            }
            string substituted = SubstituteFirstNumber(baseLocalized, newPercent);
            if (substituted == baseLocalized) return text;
            return text.Replace(baseLocalized, substituted);
        }

        /// <summary>
        /// Returns the localized vanilla charattribute string with its first integer replaced by
        /// the given value. Used for building partial penalty descriptions in the player's locale
        /// (e.g., for Heavyhanded's three-stat partial display). Returns the empty string if the
        /// lang key isn't found.
        /// </summary>
        private static string LocalizedCharAttributeWithValue(string statKey, double baseValue, int newPercent)
        {
            string baseLangKey = "charattribute-" + statKey + "-" +
                baseValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string baseLocalized = Lang.Get(baseLangKey);
            if (string.IsNullOrEmpty(baseLocalized) || baseLocalized == baseLangKey)
            {
                return "";
            }
            return SubstituteFirstNumber(baseLocalized, newPercent);
        }

        /// <summary>
        /// Builds a fully localized trait line `{traitName} <font opacity="0.6">({desc})</font>`
        /// using vanilla's `traitwithattributes` template, with the trait name pulled from the
        /// vanilla `trait-{code}` lang key (so it shows up in the player's locale: "Hardy" in EN,
        /// "Robuste" in FR, etc.) and the description from one of our `seraphleveling:` lang
        /// values. The seraphleveling lang values store only the inner description text — the
        /// trait label and font wrapper come from this helper.
        /// </summary>
        private static string BuildLocalizedTraitLine(string vanillaTraitCode, string seraphDescLangKey, params object[] descArgs)
        {
            string traitName = Lang.Get("trait-" + vanillaTraitCode);
            string desc = Lang.Get(seraphDescLangKey, descArgs);
            return Lang.Get("traitwithattributes", traitName, desc);
        }

        // Pattern for finding the first integer (or decimal) in a localized charattribute string.
        // Used to swap the percentage when combining vanilla + progression bonuses, since most
        // languages place the value as the first numeric token in the string.
        private static readonly System.Text.RegularExpressions.Regex FirstNumberRegex =
            new System.Text.RegularExpressions.Regex(@"\d+(?:\.\d+)?", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string SubstituteFirstNumber(string text, int newValue)
        {
            var match = FirstNumberRegex.Match(text);
            if (!match.Success) return text;
            return text.Substring(0, match.Index) + newValue.ToString() + text.Substring(match.Index + match.Length);
        }

        /// <summary>
        /// Collapses adjacent bullet markers ("•") separated only by whitespace
        /// and/or font open/close tags into a single bullet. Same-line only; bullets
        /// separated by a newline (i.e. different trait entries) are left alone.
        /// Iterates so runs of 3+ bullets collapse fully; bounded by a safety counter.
        /// </summary>
        private static string CollapseDuplicateBullets(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string previous;
            int safety = 0;
            do
            {
                previous = text;
                text = DuplicateBulletPair.Replace(text, m => "•" + m.Groups["between"].Value);
                safety++;
            }
            while (text != previous && safety < 8);

            return text;
        }

        public static bool HasNoTraits(string text)
        {
            // Get the "no traits" message - vanilla uses this for classes like Commoner.
            // The actual vanilla lang key is the literal phrase "No positive or negative traits"
            // (yes, the key contains spaces). The previous "charactersheet-notraits" key doesn't
            // exist in vanilla, so Lang.Get returned the key string itself, defeating the
            // localization check. Subsequent `Contains("No positive or negative traits")`
            // fallbacks below are kept as a defense-in-depth English match.
            string noTraitsMsg = Lang.Get(NO_TRAITS_KEY);

            // Check if we have NO real traits (only "no traits" message or empty)
            // Use Contains to handle cases where the message might have formatting
            bool hasNoTraits = string.IsNullOrEmpty(text) ||
                               text.Trim() == noTraitsMsg.Trim() ||
                               text == noTraitsMsg ||
                               text.Contains(noTraitsMsg) ||
                               text.Contains(NO_TRAITS_KEY);
            
            return hasNoTraits;
        }
    }
}
