using System;
using System.IO;

namespace SeraphLeveling
{
    public abstract class RemovalProgressData<T> : ProgressData<T> where T : ProgressData<T>, IProgressDataContract<T>
    {
        public bool IsRemoved { get; set; }
        public RemovalProgressData()
        {
            IsRemoved = false;
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(IsRemoved);
        }
    }
}