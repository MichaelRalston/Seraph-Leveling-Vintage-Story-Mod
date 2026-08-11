using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public abstract record class LeveledAttributeModifierDefinition<D, PD>  : AttributeModifierDefinition<D, PD> where PD : LeveledAttributeModifierProgressData<D, PD> where D: LeveledAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required string SkillKey { get; init; }
        public required string Name { get; init; }
        public required string Stat { get; init; }
        public required string LongDescription { get; init; }
        public required int GlobalMaxCredits { get; set; }
        public override int PersistenceVersion { get; init; } = 2;

        public virtual int GetMaxCredits(EntityPlayer player) => GlobalMaxCredits;

        public abstract int ApplyBonus(IServerPlayer player, PD progressData);

        public abstract int CalculateBonus(EntityPlayer entity, PD progress);

        public void ApplyBonusIfExists(IServerPlayer player) {
            if (ProgressDictionary.TryGetValue(player.PlayerUID, out var progress))
                ApplyBonus(player, (PD)progress);
        }

        public virtual void CheckUnlocks(IServerPlayer player)
        {
        }
        public void MaxStat(IServerPlayer player)
        {
            var progress = GetDict(player);
            int maxCredits = GetMaxCredits(player.Entity);
            progress.TotalCredits = maxCredits;
            progress.ZeroPartialCredit();
            MarkForSave(true);
            ApplyBonus(player, progress);
        }
        public void ApplyTraitTestSuite1Command(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.TotalCredits = 1;
            progress.ZeroPartialCredit();
            MarkForSave(true);
            ApplyBonus(player, progress);
        }

        public TextCommandResult HandleTraitCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            var progress = GetDict(player);

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateBonus(player.Entity, progress);
            int maxCredits = GetMaxCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"{Name} progression: {currentCredits}% / {maxCredits}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}{Stat}");
            progress.WriteIncrementLine(sb);

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

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
                return TextCommandResult.Success($"Current {Description} level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}{Stat})");
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
            progress.TotalCredits = level;
            MarkForSave(true);
            ApplyBonus(player, progress);
            progress.UpdateSkillActivityDay();
            return TextCommandResult.Success($"{Name} level set to {level} (+{level}{Stat}) for {player.PlayerName}.");
        }

        public TextCommandResult HandleMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    // FIXME This shouldn't be walking-specific
                    return TextCommandResult.Error("Max walking speed percent must be at least 1");
                }

                GlobalMaxCredits = newValue.Value;
                MarkForSave(true);

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in SeraphLevelingModSystem.ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    var progress = GetDict(player);
                    ApplyBonus(player, progress);
                }

                return TextCommandResult.Success($"Max {LongDescription} bonus set to +{GlobalMaxCredits}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max {LongDescription} bonus: +{GlobalMaxCredits}%");
            }
        }

        public void GetTraitAllCommandLine(IPlayer player, StringBuilder sb)
        {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TotalCredits}/{GetMaxCredits(player.Entity)} (+{progress.TotalCredits}{Stat})");
        }

        public override void HandleLogin(IServerPlayer player)
        {
            var progress = GetDict(player);
            ApplyBonus(player, progress);
            if (progress.TotalCredits > 0)
            {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Applied {Description} bonus {progress.TotalCredits}% to player {player.PlayerName}");
            }
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

            Definition.MarkForSave(true);
            int bonusPercent = Definition.ApplyBonus(player, (PD)this);
            UpdateSkillActivityDay();

            return TextCommandResult.Success($"{Definition.Name} credits set to {newCredits} (+{bonusPercent}{Definition.Stat}).");
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
