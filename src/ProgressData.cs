using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;

namespace SeraphLeveling
{
    public interface IProgressDataContract<T> where T: IProgressDataContract<T>
    {
        public virtual static byte[] GetHeader()
        {
            return Encoding.ASCII.GetBytes(T.GetHeaderString());
        }
        
        public abstract static string GetHeaderString();
        public abstract static byte GetVersion();
        public abstract static void MarkForSave();
        public static virtual string SAVE_KEY { get; }
        public static virtual string Description { get; }
        public abstract static T ReadVersion(byte version, BinaryReader reader);
        public abstract static ref ConcurrentDictionary<string, T> ProgressDictionary();
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