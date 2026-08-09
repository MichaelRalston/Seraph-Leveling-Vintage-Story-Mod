using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Server;

namespace SeraphLeveling
{
    public record class AttributeModifierDefinition
    {
        public required string Id { get; init; }
        public required Func<AttributeModifierDefinition, byte, AAttributeModifierProgressData> ProgressDataFactory { get; init; }
        public required string SaveKey { get; init; }
        public required string Description { get; init; }
        public required string PersistenceHeader { get; init; }
        public virtual int PersistenceVersion { get; } = 1;

        public byte[] PersistenceHeaderBytes => Encoding.ASCII.GetBytes(PersistenceHeader);
        public ConcurrentDictionary<string, AAttributeModifierProgressData> ProgressDictionary => SeraphLevelingModSystem.ProgressData.GetOrAdd(this, _ => []);

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
                        var progressData = ProgressDataFactory(this, version);

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
    }

    public record class LeveledAttributeModifierDefinition : AttributeModifierDefinition
    {
        public required string Name { get; init; }
        public required string Stat { get; init; }
        public required string LongDescription { get; init; }
        public required int GlobalMaxCredits { get; set; }
        public override int PersistenceVersion { get; } = 2;
    }
}
