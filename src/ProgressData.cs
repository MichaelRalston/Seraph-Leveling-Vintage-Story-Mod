using System;
using System.IO;

namespace SeraphLeveling
{
    public interface IProgressDataContract<T> where T: IProgressDataContract<T>
    {
        public abstract static byte[] GetHeader();
        public abstract static byte GetVersion();
        public static virtual string SAVE_KEY { get; }
        public static virtual string Description { get; }
        public abstract static T ReadVersion(byte version, BinaryReader reader);
    }

    public abstract class ProgressData<T> where T: ProgressData<T>, IProgressDataContract<T>
    {
        public abstract void WriteOut(BinaryWriter writer);
        public static void WriteHeader(BinaryWriter writer) {
            var header = T.GetHeader();
            foreach (var b in header) {
                writer.Write(b);
            }
            writer.Write(T.GetVersion());
        }
        public static bool ReadHeader(BinaryReader reader) {
            var header = T.GetHeader();
            bool hasProblem = false;
            foreach (var b in header) {
                byte bin = reader.ReadByte();
                hasProblem |= (bin != b);
            }
            return !hasProblem;
        }
    }
}