using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace SeraphLeveling
{
    public interface ISaveableAttribute
    {

    }
    public abstract record class AttributeModifierDefinition<T, PD>: ISaveableAttribute where T : AttributeModifierDefinition<T, PD> where PD : AAttributeModifierProgressData<T, PD>
    {
        public required string Id { get; init; }
        public required string SaveKey { get; init; }
        public required string SkillKey { get; init; }
        public required string Description { get; init; }
        public required string PersistenceHeader { get; init; }
        public virtual int PersistenceVersion { get; } = 1;

        public byte[] PersistenceHeaderBytes => Encoding.ASCII.GetBytes(PersistenceHeader);
        public ConcurrentDictionary<string, PD> ProgressDictionary
        {
            get
            {
                var innerDict = SeraphLevelingModSystem.ProgressData.GetOrAdd(
                    this,
                    _ => new ConcurrentDictionary<string, IAttributeModifierProgressData>()
                );
                return (ConcurrentDictionary<string, PD>)(object)innerDict;
            }
        }

        private static readonly object persistLock = new object();

        protected abstract PD CreateProgressData();

        public bool IsSavePending()
        {
            return SeraphLevelingModSystem.PendingSaves.GetValueOrDefault(this, false);
        }

        public void MarkForSave()
        {
            SeraphLevelingModSystem.PendingSaves.AddOrUpdate(this, true, (_, _) => true);
        }

        public virtual void LoadProgress(ICoreServerAPI serverApi)
        {
            if (serverApi == null)
            {
                return;
            }

            var progress = ProgressDictionary;
            progress.Clear();

            try
            {
                byte[] data = serverApi.WorldManager.SaveGame.GetData(SaveKey);
                if (data == null || data.Length == 0)
                {
                    serverApi.Logger.Debug($"[SeraphLeveling] No {Description} progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        if (!ReadHeader(reader)) {
                            serverApi.Logger.Warning($"[SeraphLeveling] Invalid {Description} progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        var progressData = CreateProgressData();

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                progressData.ReadVersion(version, reader);
                                progress[playerUid] = progressData;
                            }
                            catch (Exception innerEx)
                            {
                                serverApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in {Description} data: {innerEx.Message}");
                                break;
                            }
                        }
                        if (version != PersistenceVersion) {
                            MarkForSave();
                        }
                    }
                }

                serverApi.Logger.Notification($"[SeraphLeveling] Loaded {Description} progress for {progress.Count} players");
            }
            catch (Exception ex)
            {
                serverApi.Logger.Error($"[SeraphLeveling] Failed to load {Description} progress: {ex.Message}");
            }
        }

        public virtual void PersistProgress(ICoreServerAPI serverApi)
        {
            if (serverApi == null) return;
            var progress = ProgressDictionary;

            lock (persistLock)
            {
                if (progress.IsEmpty)
                {
                    return;
                }

                try
                {
                    var snapshot = progress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            WriteHeader(writer);

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var p = playerKvp.Value;
                                p.WriteOut(writer);
                            }
                        }
                        data = ms.ToArray();
                    }

                    serverApi.WorldManager.SaveGame.StoreData(SaveKey, data);
                    serverApi.Logger.Debug($"[SeraphLeveling] Persisted {Description} progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    serverApi.Logger.Error($"[SeraphLeveling] Failed to persist {Description} progress: {ex.Message}");
                }
            }
        }

        private bool ReadHeader(BinaryReader reader)
        {
            bool hasProblem = false;
            foreach (var b in PersistenceHeaderBytes)
            {
                byte bin = reader.ReadByte();
                hasProblem |= (bin != b);
            }
            return !hasProblem;
        }
        public void WriteHeader(BinaryWriter writer) {
            foreach (var b in PersistenceHeaderBytes) {
                writer.Write(b);
            }
            writer.Write(PersistenceVersion);
        }

        public virtual int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb) {
            return 0;
        }
        public virtual int ApplyDeathPenalty(IServerPlayer player, StringBuilder sb) {
            return 0;
        }

        public abstract int ApplyBonus(IServerPlayer player, PD progressData);

        public void ApplyBonusIfExists(IServerPlayer player) {
            if (ProgressDictionary.TryGetValue(player.PlayerUID, out var progress))
                ApplyBonus(player, progress);
        }

        protected PD GetDict(IPlayer player) {
            return ProgressDictionary.GetOrAdd(player.PlayerUID, _ => CreateProgressData());
        }

    }

    public abstract record class LeveledAttributeModifierDefinition : AttributeModifierDefinition<LeveledAttributeModifierDefinition, LeveledAttributeModifierProgressData>
    {
        public required string Name { get; init; }
        public required string Stat { get; init; }
        public required string LongDescription { get; init; }
        public required int GlobalMaxCredits { get; set; }
        public override int PersistenceVersion { get; } = 2;
        public required int BaseIncrement { get; init; }
        public required int IncrementStep { get; init; }

        protected override LeveledAttributeModifierProgressData CreateProgressData() => new(this);

        public virtual int GetMaxCredits(EntityPlayer player) => GlobalMaxCredits;

        public override int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb) {
            if (!SeraphLevelingModSystem.DecayExemptSkills.Contains(SkillKey) && !SeraphLevelingModSystem.DisabledSkills.Contains(SkillKey))
            {
                if (ProgressDictionary.TryGetValue(player.PlayerUID, out var progressB) && (progressB is LeveledAttributeModifierProgressData progress) && (progress.TotalCredits > 0 || progress.PartialCredit > 0))
                {
                    var (grace, basePoints, maxPoints) = SeraphLevelingModSystem.GetDecayParams(SkillKey);
                    int decayCredits = SeraphLevelingModSystem.CalculateDecayPoints(progress.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        return progress.ApplyStatPenalty(decayCredits, sb, verboseSb);
                    }
                }
            }
            return 0;
        }

        public override int ApplyDeathPenalty(IServerPlayer player, StringBuilder sb) {
            if (!SeraphLevelingModSystem.DeathPenaltyExemptSkills.Contains(SkillKey) && !SeraphLevelingModSystem.DisabledSkills.Contains(SkillKey))
            {
                if (ProgressDictionary.TryGetValue(player.PlayerUID, out var progressB) && (progressB is LeveledAttributeModifierProgressData progress) && (progress.TotalCredits > 0 || progress.PartialCredit > 0))
                {
                    double rawPenalty = BaseIncrement * SeraphLevelingModSystem.DeathPenaltyFraction * Math.Sqrt(Math.Max(1, progress.TotalCredits));
                    return progress.ApplyStatPenalty(rawPenalty, sb, null);
                }
            }
            return 0;
        }

        public void ResetProgress(IServerPlayer player) {
            var progress = GetDict(player);
            progress.TotalCredits = 0;
            progress.PartialCredit = 0;
            progress.CurrentIncrementSize = BaseIncrement;
            progress.LastActivityDay = 0;
            MarkForSave();
            ApplyBonus(player, progress);
        }
        public void MaxStat(IServerPlayer player) {
            var progress = GetDict(player);
            int maxCredits = GetMaxCredits(player.Entity);
            progress.TotalCredits = maxCredits;
            progress.PartialCredit = 0;
            MarkForSave();
            ApplyBonus(player, progress);
        }

    }
}
