using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;

namespace SeraphLeveling.Data.Legacy
{
    /// <summary>
    /// Tracks progress for a specific armor piece (for armor progression).
    /// Each armor piece tracks time worn, damage blocked, repairs, and first-equip bonus.
    /// </summary>
    public class ArmorPieceProgressData
    {
        /// <summary>Seconds worn in this armor piece toward next time credit.</summary>
        public float SecondsWornInIncrement { get; set; }

        /// <summary>Seconds needed for next time credit with this armor piece (2880, 5760, etc.).</summary>
        public int CurrentTimeIncrementSize { get; set; }

        /// <summary>Time credits earned with this armor piece.</summary>
        public int TimeCredits { get; set; }

        /// <summary>Damage blocked toward next damage credit with this armor piece.</summary>
        public float DamageBlockedInIncrement { get; set; }

        /// <summary>Damage needed for next damage credit with this armor piece (100, 200, etc.).</summary>
        public int CurrentDamageIncrementSize { get; set; }

        /// <summary>Damage credits earned with this armor piece.</summary>
        public int DamageCredits { get; set; }

        /// <summary>Repairs done toward next repair credit with this armor piece.</summary>
        public int RepairsInIncrement { get; set; }

        /// <summary>Repairs needed for next repair credit with this armor piece (1, 2, etc.).</summary>
        public int CurrentRepairIncrementSize { get; set; }

        /// <summary>Repair credits earned with this armor piece.</summary>
        public int RepairCredits { get; set; }

        /// <summary>Whether this armor piece has been equipped before (for first-equip bonus).</summary>
        public bool HasBeenEquipped { get; set; }

        public ArmorPieceProgressData()
        {
            SecondsWornInIncrement = 0;
            CurrentTimeIncrementSize = 2880; // 1 VS day (48 minutes) in seconds
            TimeCredits = 0;
            DamageBlockedInIncrement = 0;
            CurrentDamageIncrementSize = 100; // Base damage for first credit
            DamageCredits = 0;
            RepairsInIncrement = 0;
            CurrentRepairIncrementSize = 1; // Base repairs for first credit
            RepairCredits = 0;
            HasBeenEquipped = false;
        }

        public ArmorPieceProgressData Clone()
        {
            return new ArmorPieceProgressData
            {
                SecondsWornInIncrement = this.SecondsWornInIncrement,
                CurrentTimeIncrementSize = this.CurrentTimeIncrementSize,
                TimeCredits = this.TimeCredits,
                DamageBlockedInIncrement = this.DamageBlockedInIncrement,
                CurrentDamageIncrementSize = this.CurrentDamageIncrementSize,
                DamageCredits = this.DamageCredits,
                RepairsInIncrement = this.RepairsInIncrement,
                CurrentRepairIncrementSize = this.CurrentRepairIncrementSize,
                RepairCredits = this.RepairCredits,
                HasBeenEquipped = this.HasBeenEquipped
            };
        }
    }

    /// <summary>
    /// Data structure for tracking armor progression with per-piece progress.
    /// Armor XP comes from: first-equip bonus, time worn, damage blocked, and repairs.
    /// </summary>
    public class ArmorProgressData : ProgressData<ArmorProgressData>, IProgressDataContract<ArmorProgressData>
    {
        /// <summary>Total durability credits earned (each = 1% armor durability bonus).</summary>
        public int TotalDurabilityCredits { get; set; }

        /// <summary>Total walk speed penalty reduction credits earned (each = 1% reduction).</summary>
        public int TotalWalkSpeedCredits { get; set; }

        /// <summary>Total hunger reduction credits earned (each = 1% hunger rate reduction). Optional feature.</summary>
        public int TotalHungerReductionCredits { get; set; }

        /// <summary>Total healing effectiveness credits earned (each = 1% healing bonus). Optional feature.</summary>
        public int TotalHealingCredits { get; set; }

        /// <summary>Per-armor piece progress tracking. Key is armor code (e.g., "game:armor-body-plate-iron").</summary>
        public ConcurrentDictionary<string, ArmorPieceProgressData> ArmorProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public ArmorProgressData()
        {
            TotalDurabilityCredits = 0;
            TotalWalkSpeedCredits = 0;
            TotalHungerReductionCredits = 0;
            TotalHealingCredits = 0;
            ArmorProgress = new ConcurrentDictionary<string, ArmorPieceProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific armor piece.
        /// </summary>
        public ArmorPieceProgressData GetArmorProgress(string armorCode)
        {
            if (!ArmorProgress.TryGetValue(armorCode, out var progress))
            {
                progress = new ArmorPieceProgressData
                {
                    CurrentTimeIncrementSize = SeraphLevelingModSystem.BaseSecondsInArmorPerIncrement,
                    CurrentDamageIncrementSize = SeraphLevelingModSystem.BaseDamageBlockedPerIncrement,
                    CurrentRepairIncrementSize = SeraphLevelingModSystem.BaseRepairsPerIncrement
                };
                ArmorProgress[armorCode] = progress;
            }
            return progress;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public ArmorProgressData Clone()
        {
            var clone = new ArmorProgressData
            {
                TotalDurabilityCredits = this.TotalDurabilityCredits,
                TotalWalkSpeedCredits = this.TotalWalkSpeedCredits,
                TotalHungerReductionCredits = this.TotalHungerReductionCredits,
                TotalHealingCredits = this.TotalHealingCredits,
                LastActivityDay = this.LastActivityDay,
                ArmorProgress = new ConcurrentDictionary<string, ArmorPieceProgressData>()
            };
            foreach (var kvp in this.ArmorProgress)
            {
                clone.ArmorProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }

        public static ArmorProgressData ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    var data = new ArmorProgressData
                    {
                        TotalDurabilityCredits = reader.ReadInt32(),
                        TotalWalkSpeedCredits = reader.ReadInt32(),
                    };
                    for (int i = 0; i < reader.ReadInt32(); i++)
                    {
                        string armorCode = reader.ReadString();
                        var armorProgress = new ArmorPieceProgressData
                        {
                            SecondsWornInIncrement = reader.ReadSingle(),
                            CurrentTimeIncrementSize = reader.ReadInt32(),
                            TimeCredits = reader.ReadInt32(),
                            DamageBlockedInIncrement = reader.ReadSingle(),
                            CurrentDamageIncrementSize = reader.ReadInt32(),
                            DamageCredits = reader.ReadInt32(),
                            RepairsInIncrement = reader.ReadInt32(),
                            CurrentRepairIncrementSize = reader.ReadInt32(),
                            RepairCredits = reader.ReadInt32(),
                            HasBeenEquipped = reader.ReadBoolean()
                        };
                        data.ArmorProgress[armorCode] = armorProgress;
                    }
                    return data;
                case 2:
                    var progress = new ArmorProgressData
                    {
                        TotalDurabilityCredits = reader.ReadInt32(),
                        TotalWalkSpeedCredits = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };

                    // Read per-armor progress
                    for (int j = 0; j < reader.ReadInt32(); j++)
                    {
                        string armorCode = reader.ReadString();
                        var armorProg = new ArmorPieceProgressData
                        {
                            SecondsWornInIncrement = reader.ReadSingle(),
                            CurrentTimeIncrementSize = reader.ReadInt32(),
                            TimeCredits = reader.ReadInt32(),
                            DamageBlockedInIncrement = reader.ReadSingle(),
                            CurrentDamageIncrementSize = reader.ReadInt32(),
                            DamageCredits = reader.ReadInt32(),
                            RepairsInIncrement = reader.ReadInt32(),
                            CurrentRepairIncrementSize = reader.ReadInt32(),
                            RepairCredits = reader.ReadInt32(),
                            HasBeenEquipped = reader.ReadBoolean()
                        };
                        progress.ArmorProgress[armorCode] = armorProg;
                    }
                    return progress;
                default:
                    throw new System.Exception($"Unsupported ArmorProgressData version: {version}");
            }
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(TotalDurabilityCredits);
            writer.Write(TotalWalkSpeedCredits);
            writer.Write(LastActivityDay);
            var armorSnapshot = ArmorProgress.ToArray();
            writer.Write(armorSnapshot.Length);
            foreach (var kvp in armorSnapshot)
            {
                writer.Write(kvp.Key);
                var progress = kvp.Value;
                writer.Write(progress.SecondsWornInIncrement);
                writer.Write(progress.CurrentTimeIncrementSize);
                writer.Write(progress.TimeCredits);
                writer.Write(progress.DamageBlockedInIncrement);
                writer.Write(progress.CurrentDamageIncrementSize);
                writer.Write(progress.DamageCredits);
                writer.Write(progress.RepairsInIncrement);
                writer.Write(progress.CurrentRepairIncrementSize);
                writer.Write(progress.RepairCredits);
                writer.Write(progress.HasBeenEquipped);
            }
        }

        public static string GetHeaderString()
        {
            return "SIA";
        }

        public static byte GetVersion() {
            return 2;
        }

        public static string SAVE_KEY => "sitArmorProgress";
        public static string Description => "armor";
        public static void MarkForSave() {
        }

        public static ConcurrentDictionary<string, ArmorProgressData> progressDict = [];
        public static ref ConcurrentDictionary<string, ArmorProgressData> ProgressDictionary() {
            return ref progressDict;
        }
    }
}
