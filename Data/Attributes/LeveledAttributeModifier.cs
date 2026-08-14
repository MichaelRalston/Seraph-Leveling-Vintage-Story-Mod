using System;
using System.Text;
using SeraphLeveling.Patches;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public interface ILeveledAttributeModifierDefinition
    {
        public string Name { get; }
        public string StatName { get; }
        public int GetCreditsForPlayer(IPlayer player);
        public bool IsLeveledForPlayer(IPlayer player, int requiredCredits) => GetCreditsForPlayer(player) >= requiredCredits;
        public int GetBonusPercent(EntityPlayer player);

        /// <summary>
        /// Registers a method to be called every time the credit total for this attribute changes for a player
        /// </summary>
        public event CreditsChangedDelegate CreditsChanged;
    }

    public delegate void CreditsChangedDelegate(IServerPlayer player, int oldCredits, int newCredits);

    public abstract record class LeveledAttributeModifierDefinition<D, PD> : AttributeModifierDefinition<D, PD>, ILeveledAttributeModifierDefinition where PD : LeveledAttributeModifierProgressData<D, PD> where D : LeveledAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required string SkillKey { get; init; }
        public required string Name { get; init; }
        public required string Stat { get; init; }
        public required string LongDescription { get; init; }
        public required int GlobalMaxCredits { get; set; }
        public override byte PersistenceVersion { get; init; } = 2;
        public virtual string StatCode
        {
            get => field ??= $"sit{Name}Bonus"; init;
        }
        public virtual string WatchedLevel
        {
            get => field ??= $"sit{Name}Level"; init;
        }
        public virtual string WatchedBonus
        {
            get => field ??= $"sit{Name}BonusPercent"; init;
        }
        public virtual string TraitCode
        {
            get => field ??= $"sit{SkillKey}mastery"; init;
        }
        public required string StatName { get; init; }
        public required int BaseIncrement { get; set; }
        public required int IncrementStep { get; set; }
        public required string IncrementUnits { get; init; }

        public event CreditsChangedDelegate CreditsChanged;
        public void OnCreditsChanged(IServerPlayer player, int oldCredits, PD progress)
        {
            if (oldCredits != progress.TotalCredits)
            {
                CreditsChanged?.Invoke(player, oldCredits, progress.TotalCredits);
            }
        }

        public int GetCreditsForPlayer(IPlayer player)
        {
            return GetDict(player).TotalCredits;
        }

        public virtual int GetMaxCredits(EntityPlayer player) => GlobalMaxCredits;

        public int CalculateLevelFromTraits(EntityPlayer entity)
        {
            int totalLevelFromTraits = 0;
            if (SeraphLevelingModSystem.TraitsForAttributes.TryGetValue(Id, out var traitList))
            {
                foreach (var (trait, value) in traitList)
                {
                    // TODO: use cache?
                    if (SeraphLevelingModSystem.PlayerHasTrait(entity, trait))
                    {
                        totalLevelFromTraits += value;
                    }
                }
            }
            return totalLevelFromTraits;
        }
        public virtual int ApplyBonus(IServerPlayer player, PD progressData)
        {
            if (player?.Entity == null) return 0;

            int totalLevelFromTraits = CalculateLevelFromTraits(player.Entity);
            // Calculate raw bonus from level (1% per level)
            float rawBonus = progressData.TotalCredits * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed max earnable.
            float maxEarnableBonus = (GlobalMaxCredits - totalLevelFromTraits) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));
            int bonusPercent = (int)(bonus * 100);

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set(StatName, StatCode, bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WatchedLevel, -1);
            int oldBonus = watchedAttrs.GetInt(WatchedBonus, -1);

            bool valuesChanged = (oldLevel != progressData.TotalCredits) || (oldBonus != bonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WatchedLevel, progressData.TotalCredits);
                watchedAttrs.SetInt(WatchedBonus, bonusPercent);

                SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, TraitCode, progressData.TotalCredits > 0);

                watchedAttrs.MarkPathDirty(WatchedLevel);
            }

            return bonusPercent;
        }

        public virtual int CalculateBonus(EntityPlayer entity, PD progress)
        {
            int totalLevelFromTraits = CalculateLevelFromTraits(entity);
            int earnableBonus = Math.Max(0, GlobalMaxCredits - totalLevelFromTraits);
            return Math.Min(progress.TotalCredits, earnableBonus);
        }

        public override void ApplyBonusIfExists(IServerPlayer player)
        {
            if (ProgressDictionary.TryGetValue(player.PlayerUID, out var progress))
                ApplyBonus(player, (PD)progress);
        }

        public override void MaxStat(IServerPlayer player)
        {
            var progress = GetDict(player);
            int maxCredits = GetMaxCredits(player.Entity);
            int oldCredits = progress.TotalCredits;
            progress.TotalCredits = maxCredits;
            OnCreditsChanged(player, oldCredits, progress);
            progress.ZeroPartialCredit();
            PendingSave = true;
            ApplyBonus(player, progress);
        }
        public override void ApplyTraitTestSuite1Command(IServerPlayer player)
        {
            var progress = GetDict(player);
            int oldCredits = progress.TotalCredits;
            progress.TotalCredits = 1;
            OnCreditsChanged(player, oldCredits, progress);
            progress.ZeroPartialCredit();
            PendingSave = true;
            ApplyBonus(player, progress);
        }

        public override void GetTraitAllCommandLine(IPlayer player, StringBuilder sb)
        {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TotalCredits}/{GetMaxCredits(player.Entity)} ({Direction}{progress.TotalCredits}{Stat})");
        }
        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            var progress = GetDict(player);
            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateBonus(player.Entity, progress);
            int maxCredits = GetMaxCredits(player.Entity);

            sb.AppendLine($"{Name} progression: {currentCredits}% / {maxCredits}%");
            sb.AppendLine($"Current bonus: {Direction}{bonusPercent}{Stat}");
            progress.WriteIncrementLine(sb);

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }
        }

        public override IChatCommand RegisterCommands(ICoreServerAPI api, IChatCommand c)
        {
            return c.BeginSubCommand($"{SkillKey}")
                .WithDescription($"View your {LongDescription} progression stats")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(HandleTraitCommand)
            .EndSubCommand()
            .BeginSubCommand($"{SkillKey}level")
                .WithDescription($"Get or set your {Description} level (admin only)")
                .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(HandleLevelCommand)
            .EndSubCommand()
            .BeginSubCommand($"{SkillKey}max")
                .WithDescription($"Get or set the max {LongDescription} bonus percent (admin only)")
                .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(HandleMaxCommand)
            .EndSubCommand()
            .BeginSubCommand($"{SkillKey}base")
                .WithDescription($"Get or set the base {IncrementUnits} per level (admin only)")
                .WithArgs(api.ChatCommands.Parsers.OptionalInt(IncrementUnits))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitBaseCommand)
            .EndSubCommand()
            .BeginSubCommand($"{SkillKey}increment")
                .WithDescription($"Get or set the {Description} increment step per credit (admin only)")
                .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitIncrementCommand)
            .EndSubCommand();
            ;
        }

        public TextCommandResult HandleTraitCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            var sb = new StringBuilder();
            CollectStatus(player, sb);

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        public TextCommandResult HandleLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            var progress = GetDict(player);

            int? newCredits = (int?)args[0];
            int maxCredits = GetMaxCredits(player.Entity);

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateBonus(player.Entity, progress);
                return TextCommandResult.Success($"Current {Description} level: {progress.TotalCredits}/{maxCredits} ({Direction}{currentBonus}{Stat})");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > maxCredits)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits})");
            }

            return progress.SetLevelFromCommand(player, newCredits.Value, args);
        }

        public TextCommandResult SetLevel(IServerPlayer player, int level)
        {
            var progress = GetDict(player);
            int maxLevel = GetMaxCredits(player.Entity);
            if (level > maxLevel) return TextCommandResult.Error($"Level cannot exceed max ({maxLevel}).");
            int oldCredits = progress.TotalCredits;
            progress.TotalCredits = level;
            OnCreditsChanged(player, oldCredits, progress);
            PendingSave = true;
            ApplyBonus(player, progress);
            progress.UpdateSkillActivityDay();
            return TextCommandResult.Success($"{Name} level set to {level} ({Direction}{level}{Stat}) for {player.PlayerName}.");
        }

        public TextCommandResult OnTraitIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                IncrementStep = newValue.Value;
                SeraphLevelingModSystem.pendingConfigSave = true;

                return TextCommandResult.Success($"{Name} increment step set to +{IncrementStep} per credit.\nProgression: {BaseIncrement}, {BaseIncrement + IncrementStep}, {BaseIncrement + IncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current {Description} increment step: +{IncrementStep} per credit\nProgression: {BaseIncrement}, {BaseIncrement + IncrementStep}, {BaseIncrement + IncrementStep * 2}...");
            }
        }

        public TextCommandResult OnTraitBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error($"Base {IncrementUnits} per increment must be at least 1");
                }

                BaseIncrement = newValue.Value;
                SeraphLevelingModSystem.pendingConfigSave = true;

                return TextCommandResult.Success($"Base {IncrementUnits} per increment set to {BaseIncrement}. New progress will require this many {IncrementUnits} for the first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base {IncrementUnits} per increment: {BaseIncrement}\nIncrement step: +{IncrementStep} per credit");
            }
        }

        public TextCommandResult HandleMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error($"Max {LongDescription} percent must be at least 1");
                }

                GlobalMaxCredits = newValue.Value;
                PendingSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in SeraphLevelingModSystem.ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    var progress = GetDict(player);
                    ApplyBonus(player, progress);
                }

                return TextCommandResult.Success($"Max {LongDescription} bonus set to {Direction}{GlobalMaxCredits}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max {LongDescription} bonus: {Direction}{GlobalMaxCredits}%");
            }
        }

        public override void HandleLogin(IServerPlayer player)
        {
            var progress = GetDict(player);
            ApplyBonus(player, progress);
            if (progress.TotalCredits > 0)
            {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Applied {Description} bonus {Direction}{progress.TotalCredits}% to player {player.PlayerName}");
            }
        }

        public override bool ShouldDisplay(EntityPlayer player)
        {
            return player.WatchedAttributes.GetInt(WatchedLevel, 0) > 0;
        }

        public virtual int GetBonusPercent(EntityPlayer player)
        {
            var retVal = player.WatchedAttributes.GetInt(WatchedBonus, 0);
            CharacterSystemPatches.ClientApi?.Logger?.Debug($"   [Verdus] Found bonus percent for attribute {Id}: {retVal}");
            return retVal;
        }

        public override object GetLocalizedTraitTextParam(EntityPlayer player)
        {
            return player?.Player == null ? null : GetDict(player.Player).TotalCredits;
        }
    }

    public abstract class LeveledAttributeModifierProgressData<D, PD>(D definition) : AttributeModifierProgressData<D, PD>(definition) where PD : LeveledAttributeModifierProgressData<D, PD> where D : LeveledAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        /// <summary>Total credits earned (each credit = 1% bonus).</summary>
        public int TotalCredits { get; set; }
        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public void UpdateSkillActivityDay()
        {
            if (!SeraphLevelingModSystem.EnableSkillDecay) return;
            if (SeraphLevelingModSystem.ServerApi == null) return;

            LastActivityDay = SeraphLevelingModSystem.ServerApi.World.Calendar.TotalDays;
        }

        public virtual TextCommandResult SetLevelFromCommand(IServerPlayer player, int newCredits, TextCommandCallingArgs args)
        {
            // Set the player's progress
            TotalCredits = newCredits;
            ZeroPartialCredit();
            CalculateIncrementSize();

            Definition.PendingSave = true;
            int bonusPercent = Definition.ApplyBonus(player, (PD)this);
            UpdateSkillActivityDay();

            return TextCommandResult.Success($"{Definition.Name} credits set to {newCredits} ({Definition.Direction}{bonusPercent}{Definition.Stat}).");
        }
        public virtual void ZeroPartialCredit()
        {
        }
        public virtual void CalculateIncrementSize()
        {
        }
        public virtual void WriteIncrementLine(StringBuilder sb)
        {
            // Empty.
        }
    }
}
