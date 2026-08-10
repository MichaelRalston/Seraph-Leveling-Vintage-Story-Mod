using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace SeraphLeveling.Data.Legacy
{
    public interface ILeveledTraitContract<T> where T:ILeveledTraitContract<T>
    {
        public static virtual string Name { get; }
        public static virtual string Stat { get; }
        public static virtual string SkillKey { get; }
        public static virtual string LongDescription { get; }
        public static virtual int GlobalMax{get; set;}

    }
    // This class will be very unhappy if V is type anything other than int or float. Fortunately, I don't anticipate that being an issue.
    public abstract class LeveledTraitProgressData<T> : ProgressData<T>
    where T : LeveledTraitProgressData<T>, IProgressDataContract<T>, ILeveledTraitContract<T>, new()
    {
        protected LeveledTraitProgressData() {
            TotalCredits = 0;
            LastActivityDay = 0;
        }

        /// <summary>Total credits earned (each credit = 1% bonus).</summary>
        public int TotalCredits { get; set; }
        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public abstract int GetMaxCredits(EntityPlayer player);
        public abstract int ApplyBonus(IServerPlayer player);
        public abstract int CalculateBonus(EntityPlayer player);

        // public void DoEvent(IServerPlayer player) {
                // // Skip all processing if already at max - completely invisible
                // var maxCredits = GetMaxCredits(player.Entity);
                // if (TotalCredits >= maxCredits) return;

                // int oldCredits = TotalCredits;

                // // Apply sleep buff multiplier to score
                // float modifiedScore = SeraphLevelingModSystem.ApplyXPMultiplier(player.PlayerUID, float.CreateTruncating(score));

                // // Add distance to progress
                // PartialCredit += V.CreateTruncating(modifiedScore);

                // // Check if we've earned any new credits
                // var incrementStep = GetIncrementStep();
                // var units = GetIncrementUnits();
                // while (PartialCredit >= V.CreateTruncating(CurrentIncrementSize) && TotalCredits < maxCredits)
                // {
                //     // Earn a credit
                //     TotalCredits++;
                //     PartialCredit -= V.CreateTruncating(CurrentIncrementSize);
                //     CurrentIncrementSize += incrementStep;

                //     SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned {T.Description} credit {TotalCredits}, next requires {CurrentIncrementSize} {units}");
                // }

                // // Mark for saving if any progress was made
                // if (PartialCredit > V.Zero || TotalCredits > oldCredits)
                // {
                //     T.MarkForSave();
                // }

                // // If credits increased, update the stat and notify player
                // if (TotalCredits > oldCredits)
                // {
                //     ApplyBonus(player);

                //     // Notify player of level up with raw improvement (shows progress even when capped)
                //     SeraphLevelingModSystem.NotifyLevelUp(player,
                //         Lang.Get($"seraphleveling:message-{T.Description}-level-up", TotalCredits, TotalCredits));
                // }

        // }

        protected static T GetDict(IPlayer player) {
            return T.ProgressDictionary().GetOrAdd(player.PlayerUID, _ => new T());
        }

        public virtual void WriteIncrementLine(StringBuilder sb)
        {
            // Empty.
        }

        public virtual void ZeroPartialCredit() {
            // Default implementation does nothing.
        }

        public virtual void CalculateIncrementSize() {
            // Default implementation does nothing.
        }


        public static void ApplyTraitTestSuite1Command(IServerPlayer player) {
            var progress = GetDict(player);
            progress.TotalCredits = 1;
            progress.ZeroPartialCredit();
            T.MarkForSave();
            progress.ApplyBonus(player);
        }

        public static void GetTraitAllCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{T.Name}: {progress.TotalCredits}/{progress.GetMaxCredits(player.Entity)} (+{progress.TotalCredits}{T.Stat})");
        }
        public static void MaxStat(IServerPlayer player) {
            var progress = GetDict(player);
            int maxCredits = progress.GetMaxCredits(player.Entity);
            progress.TotalCredits = maxCredits;
            progress.ZeroPartialCredit();
            T.MarkForSave();
            progress.ApplyBonus(player);
        }

        public void UpdateSkillActivityDay() {
            if (!SeraphLevelingModSystem.EnableSkillDecay) return;
            if (SeraphLevelingModSystem.ServerApi == null) return;

            LastActivityDay = SeraphLevelingModSystem.ServerApi.World.Calendar.TotalDays;
        }

        public virtual void CheckUnlocks(IServerPlayer player) {

        }

        public static TextCommandResult SetLevel(IServerPlayer player, int level) {
            var progress = GetDict(player);
            int maxLevel = progress.GetMaxCredits(player.Entity);
            if (level > maxLevel) return TextCommandResult.Error($"Level cannot exceed max ({maxLevel}).");
            progress.TotalCredits = level;
            T.MarkForSave();
            progress.ApplyBonus(player);
            progress.UpdateSkillActivityDay();
            return TextCommandResult.Success($"{T.Name} level set to {level} (+{level}{T.Stat}) for {player.PlayerName}.");
        }

        public static TextCommandResult HandleTraitCommand(TextCommandCallingArgs args) {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            var progress = GetDict(player);

            int currentCredits = progress.TotalCredits;
            int bonusPercent = progress.CalculateBonus(player.Entity);
            int maxCredits = progress.GetMaxCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"{T.Name} progression: {currentCredits}% / {maxCredits}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}{T.Stat}");
            progress.WriteIncrementLine(sb);

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        public virtual TextCommandResult SetLevelFromCommand(IServerPlayer player, int newCredits, TextCommandCallingArgs args) {
            // Set the player's progress
            TotalCredits = newCredits;
            ZeroPartialCredit();
            CalculateIncrementSize();

            T.MarkForSave();
            int bonusPercent = ApplyBonus(player);
            UpdateSkillActivityDay();

            return TextCommandResult.Success($"{T.Name} credits set to {newCredits} (+{bonusPercent}{T.Stat}).");

        }

        public static TextCommandResult HandleLevelCommand(TextCommandCallingArgs args) {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            var progress = GetDict(player);

            int? newCredits = (int?)args[0];
            int maxCredits = progress.GetMaxCredits(player.Entity);

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = progress.CalculateBonus(player.Entity);
                return TextCommandResult.Success($"Current {T.Description} level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}{T.Stat})");
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

        public static TextCommandResult HandleMaxCommand(TextCommandCallingArgs args) {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max walking speed percent must be at least 1");
                }

                T.GlobalMax = newValue.Value;
                T.MarkForSave();

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in SeraphLevelingModSystem.ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    var progress = GetDict(player);
                    progress.ApplyBonus(player);
                }

                return TextCommandResult.Success($"Max {T.LongDescription} bonus set to +{T.GlobalMax}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max {T.LongDescription} bonus: +{T.GlobalMax}%");
            }
        }

        public static void HandleLogin(IServerPlayer player) {
            var progress = GetDict(player);
            progress.ApplyBonus(player);
            if (progress.TotalCredits > 0)
            {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Applied {T.Description} bonus {progress.TotalCredits}% to player {player.PlayerName}");
            }
        }

        public static void ApplyBonusIfExists(IServerPlayer player) {
            if (T.ProgressDictionary().TryGetValue(player.PlayerUID, out var progress))
            progress.ApplyBonus(player);
        }
    }
}
