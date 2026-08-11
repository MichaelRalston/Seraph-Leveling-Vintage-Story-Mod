using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public interface ISaveableAttribute
    {
        public string Id { get; }
        public bool HasUnsavedProgress();
        public void MarkForSave(bool pending);
        public void PersistProgress(ICoreServerAPI serverApi);
        public void ResetProgress();
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
        public virtual int PersistenceVersion { get; init; } = 1;

        public byte[] PersistenceHeaderBytes => Encoding.ASCII.GetBytes(PersistenceHeader);
        public ConcurrentDictionary<string, PD> ProgressDictionary
        {
            get
            {
                var innerDict = SeraphLevelingModSystem.ProgressData.GetOrAdd(
                    this,
                    _ => new ConcurrentDictionary<string, PD>()
                );
                return (ConcurrentDictionary<string, PD>)innerDict;
            }
        }

        public bool HasUnsavedProgress() => !ProgressDictionary.IsEmpty;
        public void ResetProgress() => ProgressDictionary.Clear();

        private static readonly object persistLock = new object();

        public PD CreateProgressData() => D.Create((D)this);

        public PD GetForPlayer(string playerUid)
        {
            return ProgressDictionary.GetOrAdd(playerUid, _ => CreateProgressData());
        }


        public bool IsSavePending()
        {
            return SeraphLevelingModSystem.PendingSaves.GetValueOrDefault(this, false);
        }

        public void MarkForSave(bool pending)
        {
            SeraphLevelingModSystem.PendingSaves.AddOrUpdate(this, pending, (_, _) => pending);
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
                            MarkForSave(true);
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
