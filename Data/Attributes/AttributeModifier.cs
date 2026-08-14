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
    }

    public interface ISaveableAttribute : IAttribute
    {
        public bool HasUnsavedProgress();
        public void PersistProgress(ICoreServerAPI serverApi);
        public void ResetProgress();
        public bool PendingSave { get; set; }
        public void ResetProgress(IServerPlayer player);
        public void ApplyBonusIfExists(IServerPlayer player);
        public void MaxStat(IServerPlayer player);
        public void ApplyTraitTestSuite1Command(IServerPlayer player);
        public int ApplyDeathPenalty(IServerPlayer player, StringBuilder sb);
        public int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb);
        public void LoadProgress(ICoreServerAPI serverApi);
        public void HandleLogin(IServerPlayer player);
        public IChatCommand RegisterCommands(ICoreServerAPI serverApi, IChatCommand c);
    }

    public interface IConstructable<D, PD>
    {
        static abstract PD Create(D def);
    }
    
    public abstract record class AttributeModifierDefinition<D, PD> : ISaveableAttribute where D : AttributeModifierDefinition<D, PD>, IConstructable<D, PD> where PD : AttributeModifierProgressData<D, PD>
    {
        public required string Id { get; init; }
        public required string SaveKey { get; init; }
        public required string Description { get; init; }
        public virtual string Direction { get; init; } = "+";
        public required string PersistenceHeader { get; init; }
        public virtual byte PersistenceVersion { get; init; } = 1;
        public abstract void CollectStatus(IPlayer player, StringBuilder sb);
        public abstract bool ShouldDisplay(EntityPlayer player);

        public virtual void Unlock(IServerPlayer player, bool notify = false)
        {
        }

        public virtual void ApplyBonusIfExists(IServerPlayer player)
        {
            
        }

        public virtual void ApplyTraitTestSuite1Command(IServerPlayer player)
        {
            
        }

        public virtual IChatCommand RegisterCommands(ICoreServerAPI _, IChatCommand c) {
            return c;
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
            return ProgressDictionary.GetOrAdd(playerUid, _ => {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] No {Description} progress data found for {playerUid}, creating new progress dictionary for them.");
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
                    serverApi.Logger.Debug($"[SeraphLeveling] No {Description} progress data found in world save");
                    return;
                }
                else {
                    var stringyData = string.Concat(data.Select(b => b >= 32 && b <= 126 ? ((char)b).ToString() : $"[0x{b:X2}]"));
                    serverApi.Logger.Debug($"[SeraphLeveling] {Description} progress data found: {stringyData} in world save");
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        if (!ReadHeader(reader))
                        {
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
                                serverApi.Logger.Debug($"[SeraphLeveling] {Description} progress contains progress for {playerUid}");
                                progressData.ReadVersion(version, reader);
                                progress[playerUid] = progressData;
                            }
                            catch (Exception innerEx)
                            {
                                serverApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i + 1}/{playerCount} in {Description} data: {innerEx.Message}");
                                break;
                            }
                        }
                        if (version != PersistenceVersion) {
                            PendingSave = true;
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
            serverApi.Logger.Debug($"[SeraphLeveling] Entering PersistProgress for {Description} progress to go to {SaveKey}.");

            lock (SeraphLevelingModSystem.persistLock)
            {
                if (progress.IsEmpty)
                {
                    return;
                }

                try
                {
                    var snapshot = progress.ToArray();
                    foreach (var playerKvp in snapshot) {
                        serverApi.Logger.Debug($"[SeraphLeveling] {Description} progress contains progress for {playerKvp.Key}");
                    }

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
                    var stringyData = string.Concat(data.Select(b => b >= 32 && b <= 123 ? ((char)b).ToString() : $"[0x{b:X2}]"));
                    serverApi.Logger.Debug($"[SeraphLeveling] {Description} progress was stored as {stringyData}");
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

        protected PD GetDict(IPlayer player) {
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
}
