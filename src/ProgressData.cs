using System;
using System.IO;

namespace SeraphLeveling
{
    public interface IProgressDataContract
    {
        public abstract static byte[] GetHeader();
        public abstract static byte GetVersion();
        public static string SAVE_KEY { get; }
        public static string Description { get; }
    }

    public abstract class ProgressData<T> where T: ProgressData<T>, IProgressDataContract
    {
        public abstract void WriteOut(BinaryWriter writer);
        public void WriteHeader(BinaryWriter writer) {
            var header = T.GetHeader();
            foreach (var b in header) {
                writer.Write(b);
            }
            writer.Write(GetVersion());
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