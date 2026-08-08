using System;
using System.IO;

namespace SeraphLeveling {
    public abstract class LockedTraitProgressData<T> : ProgressData<T> where T : LockedTraitProgressData<T>, IProgressDataContract<T>, new()
    {
        /// <summary>Whether the trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }
        public LockedTraitProgressData()
        {
            IsUnlocked = false;
        }
        public T Clone()
        {
            return (T)this.MemberwiseClone();
        }
        public static T ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new T {
                        IsUnlocked = reader.ReadBoolean()
                    };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

        public override void WriteOut(BinaryWriter writer) {
            writer.Write(IsUnlocked);
        }

    }
}