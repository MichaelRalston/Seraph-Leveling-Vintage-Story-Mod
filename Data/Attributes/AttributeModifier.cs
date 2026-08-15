using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using System.Linq;

namespace SeraphLeveling.Data.Attributes
{
    public interface IAttribute
    {
        public string Id { get; }

        /// <summary>
        /// Unlock the attribute if it can be unlocked
        /// </summary>
        public abstract void Unlock(IServerPlayer player, bool notify = false);

        /// <summary>
        /// Get a status string for the attribute to return for the attribute when the player uses the /trait command
        /// </summary>
        public abstract void CollectStatus(IPlayer player, StringBuilder sb);

        /// <summary>
        /// Determine whether this attribute should be shown in trait text on the character screen
        /// </summary>
        public abstract bool ShouldDisplay(EntityPlayer player);

        public abstract object GetLocalizedTraitTextParam(EntityPlayer player);
    }

    public interface ISaveableAttribute : IAttribute
    {
        public bool HasUnsavedProgress();
        public void PersistProgress(ICoreServerAPI serverApi);
        public void ResetProgress();
        public bool PendingSave { get; set; }
        public string SkillKey { get; init; }
        public void ResetProgress(IServerPlayer player);
        public void ApplyBonusIfExists(IServerPlayer player);
        public void MaxStat(IServerPlayer player);
        public void ApplyTraitTestSuite1Command(IServerPlayer player);
        public void GetTraitAllCommandLine(IPlayer player, StringBuilder sb); // Only implement this or GetTraitUnlockableCommandLine.
        public void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb); // Only implement this or GetTraitUnlockableCommandLine.
        public int ApplyDeathPenalty(IServerPlayer player, StringBuilder sb);
        public int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb);
        public void LoadProgress(ICoreServerAPI serverApi);
        public void HandleLogin(IServerPlayer player);
        public TextCommandResult HandleBaseCommand(TextCommandCallingArgs args, int indexOffset = 0);
        public TextCommandResult HandleLevelCommand(TextCommandCallingArgs args, int indexOffset = 0);

        public TextCommandResult HandleMaxCommand(TextCommandCallingArgs args, int indexOffset = 0);
        public TextCommandResult HandleUnlockCommand(TextCommandCallingArgs args, int indexOffset = 0);
        public TextCommandResult HandleIncrementCommand(TextCommandCallingArgs args, int indexOffset = 0);
        public IChatCommand RegisterCommands(ICoreServerAPI serverApi, IChatCommand c);
    }

    public interface IConstructable<D, PD>
    {
        static abstract PD Create(D def);
    }

    public abstract class AttributeModifierDefinition<D, PD> : ISaveableAttribute where D : AttributeModifierDefinition<D, PD>, IConstructable<D, PD> where PD : AttributeModifierProgressData<D, PD>
    {
        public required string Id { get; init; }
        public required string SaveKey { get; init; }
        public required string SkillKey { get; init; }
        public virtual string LongDescription { get => field??SkillKey; init; }
        public virtual string Direction { get; init; } = "+";
        public required string PersistenceHeader { get; init; }
        public virtual byte PersistenceVersion { get; init; } = 1;
        public abstract void CollectStatus(IPlayer player, StringBuilder sb);
        public abstract bool ShouldDisplay(EntityPlayer player);
        public abstract object GetLocalizedTraitTextParam(EntityPlayer player);

        public virtual void Unlock(IServerPlayer player, bool notify = false)
        {
        }

        public virtual void ApplyBonusIfExists(IServerPlayer player)
        {

        }

        public virtual void ApplyTraitTestSuite1Command(IServerPlayer player)
        {

        }

        public virtual void GetTraitAllCommandLine(IPlayer player, StringBuilder sb)
        {

        }

        public virtual void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb)
        {

        }

        public virtual IChatCommand RegisterCommands(ICoreServerAPI _, IChatCommand c)
        {
            return c;
        }

        public virtual TextCommandResult HandleBaseCommand(TextCommandCallingArgs args, int indexSkip)
        {
            return TextCommandResult.Error($"{SkillKey} trait does not support setting base increment.");
        }

        public virtual TextCommandResult HandleLevelCommand(TextCommandCallingArgs args, int indexSkip)
        {
            return TextCommandResult.Error($"{SkillKey} trait does not support setting level.");
        }

        public virtual TextCommandResult HandleMaxCommand(TextCommandCallingArgs args, int indexSkip)
        {
            return TextCommandResult.Error($"{SkillKey} trait does not support setting max level.");
        }
        public virtual TextCommandResult HandleIncrementCommand(TextCommandCallingArgs args, int indexSkip)
        {
            return TextCommandResult.Error($"{SkillKey} trait does not support setting increment step.");
        }
        public virtual TextCommandResult HandleUnlockCommand(TextCommandCallingArgs args, int indexSkip)
        {
            return TextCommandResult.Error($"{SkillKey} trait does not support unlocking.");
        }

        public byte[] PersistenceHeaderBytes => Encoding.ASCII.GetBytes(PersistenceHeader);
        public ConcurrentDictionary<string, PD> ProgressDictionary { get; init; } = new ConcurrentDictionary<string, PD>();
        public bool PendingSave { get; set; } = false;

        public bool HasUnsavedProgress() => PendingSave || !ProgressDictionary.IsEmpty;
        public void ResetProgress() => ProgressDictionary.Clear();
        public virtual void ResetProgress(IServerPlayer player)
        {

        }
        public virtual void MaxStat(IServerPlayer player)
        {

        }

        public PD CreateProgressData() => D.Create((D)this);

        public PD GetForPlayer(string playerUid)
        {
            return ProgressDictionary.GetOrAdd(playerUid, _ =>
            {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] No {SkillKey} progress data found for {playerUid}, creating new progress dictionary for them.");
                return CreateProgressData();
            });
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
                    serverApi.Logger.Debug($"[SeraphLeveling] No {SkillKey} progress data found in world save");
                    return;
                }
                else {
                    #if SPAMMYDEBUG
                        var stringyData = string.Concat(data.Select(b => b >= 32 && b <= 123 ? ((char)b).ToString() : $"[0x{b:X2}]"));
                        serverApi.Logger.Debug($"[SeraphLeveling] {SkillKey} progress data found: {stringyData} in world save");
                    #endif
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        if (!ReadHeader(reader))
                        {
                            serverApi.Logger.Warning($"[SeraphLeveling] Invalid {SkillKey} progress data format");
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
                                #if SPAMMYDEBUG
                                    serverApi.Logger.Debug($"[SeraphLeveling] {SkillKey} progress contains progress for {playerUid}");
                                #endif
                                progressData.ReadVersion(version, reader);
                                progress[playerUid] = progressData;
                            }
                            catch (Exception innerEx)
                            {
                                serverApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i + 1}/{playerCount} in {SkillKey} data: {innerEx.Message}");
                                break;
                            }
                        }
                        if (version != PersistenceVersion)
                        {
                            PendingSave = true;
                        }
                    }
                }
                #if SPAMMYDEBUG
                    serverApi.Logger.Notification($"[SeraphLeveling] Loaded {SkillKey} progress for {progress.Count} players");
                #endif
            }
            catch (Exception ex)
            {
                serverApi.Logger.Error($"[SeraphLeveling] Failed to load {SkillKey} progress: {ex.Message}");
            }
        }

        public virtual void PersistProgress(ICoreServerAPI serverApi)
        {
            if (serverApi == null) return;
            var progress = ProgressDictionary;
            #if SPAMMYDEBUG
                serverApi.Logger.Debug($"[SeraphLeveling] Entering PersistProgress for {SkillKey} progress to go to {SaveKey}.");
            #endif
            lock (SeraphLevelingModSystem.persistLock)
            {
                if (progress.IsEmpty)
                {
                    return;
                }

                try
                {
                    var snapshot = progress.ToArray();
                    #if SPAMMYDEBUG
                        foreach (var playerKvp in snapshot)
                        {
                            serverApi.Logger.Debug($"[SeraphLeveling] {SkillKey} progress contains progress for {playerKvp.Key}");
                        }
                    #endif

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
                    #if SPAMMYDEBUG
                        serverApi.Logger.Debug($"[SeraphLeveling] Persisted {SkillKey} progress for {snapshot.Length} players");
                        var stringyData = string.Concat(data.Select(b => b >= 32 && b <= 123 ? ((char)b).ToString() : $"[0x{b:X2}]"));
                        serverApi.Logger.Debug($"[SeraphLeveling] {SkillKey} progress was stored as {stringyData}");
                    #endif
                }
                catch (Exception ex)
                {
                    serverApi.Logger.Error($"[SeraphLeveling] Failed to persist {SkillKey} progress: {ex.Message}");
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
        public void WriteHeader(BinaryWriter writer)
        {
            foreach (var b in PersistenceHeaderBytes)
            {
                writer.Write(b);
            }
            writer.Write(PersistenceVersion);
        }

        public virtual int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb)
        {
            return 0;
        }
        public virtual int ApplyDeathPenalty(IServerPlayer player, StringBuilder sb)
        {
            return 0;
        }

        protected PD GetDict(IPlayer player)
        {
            return ProgressDictionary.GetOrAdd(player.PlayerUID, _ => CreateProgressData());
        }

        public abstract void HandleLogin(IServerPlayer player);
    }

    public interface IAttributeModifierProgressData
    {

    }

    public abstract class AttributeModifierProgressData<D, PD> : IAttributeModifierProgressData where PD : AttributeModifierProgressData<D, PD> where D : AttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        protected D Definition { get; init; }

        public AttributeModifierProgressData(D definition)
        {
            Definition = definition;
        }

        public abstract void ReadVersion(byte version, BinaryReader reader);
        public abstract void WriteOut(BinaryWriter writer);
    }

    public delegate void ActiveStatusUpdatedDelegate(IServerPlayer player, bool newValue);

    public interface IAttributeModifier
    {
        public ISaveableAttribute Attribute { get; }
        public int ModifierValue { get; }
        public bool HasRequirements { get; }
        public string DynamicAttributeContentsKey { get; }

        public event ActiveStatusUpdatedDelegate ActiveStatusUpdated;

        public bool IsActive(IPlayer player);

        /// <summary>
        /// Get a status string for the attribute modifier's requirements when the player uses the /trait command
        /// </summary>
        public abstract void CollectRequirementStatus(IPlayer player, StringBuilder sb);

        public static IAttributeModifier Bonus(ISaveableAttribute attribute, int absModifierValue, List<IAttributeRequirement> unlockWith = null)
        {
            if (absModifierValue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(absModifierValue), absModifierValue, "Modifier value must be given as a positive");
            }
            return new Instance(attribute, absModifierValue, unlockWith, null);
        }

        public static IAttributeModifier Penalty(ISaveableAttribute attribute, int absModifierValue, List<IAttributeRequirement> removeWith = null)
        {
            if (absModifierValue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(absModifierValue), absModifierValue, "Modifier value must be given as a positive");
            }
            return new Instance(attribute, -absModifierValue, null, removeWith);
        }

        private record class Instance : IAttributeModifier
        {
            public Instance(ISaveableAttribute attribute, int modifierValue, List<IAttributeRequirement> unlockWith = null, List<IAttributeRequirement> removeWith = null)
            {
                Attribute = attribute;
                ModifierValue = modifierValue;
                UnlockWith = unlockWith ?? [];
                UnlockWith.ForEach(req => req.SatisfactionChanged += OnRequirementSatisfactionChanged);
                RemoveWith = removeWith ?? [];
                RemoveWith.ForEach(req => req.SatisfactionChanged += OnRequirementSatisfactionChanged);
            }

            public ISaveableAttribute Attribute { get; init; }

            public int ModifierValue { get; init; }
            private List<IAttributeRequirement> UnlockWith { get; init; }
            private List<IAttributeRequirement> RemoveWith { get; init; }
            public bool HasRequirements => UnlockWith.Count > 0 || RemoveWith.Count > 0;
            public string DynamicAttributeContentsKey
            {
                get => field ??= $"seraphleveling:attribute-{Attribute.Id}-contents"; init;
            }

            public event ActiveStatusUpdatedDelegate ActiveStatusUpdated;

            public void CollectRequirementStatus(IPlayer player, StringBuilder sb)
            {
                // Requirement output is specifically for unlocking bonus traits, not removing penalties
                UnlockWith.ForEach(req => req.CollectStatus(player, sb));
            }

            public bool IsActive(IPlayer player)
            {
                if (player?.Entity == null)
                {
                    return false;
                }
                else if (RemoveWith.Any(req => !req.IsSatisfied(player)))
                {
                    // If at least one removal requirement is present and any are unsatisfied, then the modifier remains active
                    return Attribute.ShouldDisplay(player.Entity);
                }
                else if (UnlockWith.All(req => req.IsSatisfied(player)))
                {
                    // If all unlock requirements are met, or none are specified, then the modifier becomes active
                    return Attribute.ShouldDisplay(player.Entity);
                }
                else
                {
                    // Otherwise, the modifier is inactive
                    return false;
                }
            }

            private void OnRequirementSatisfactionChanged(IServerPlayer player, bool oldValue, bool newValue)
            {
                ActiveStatusUpdated?.Invoke(player, IsActive(player));
            }
        }
    }
}
