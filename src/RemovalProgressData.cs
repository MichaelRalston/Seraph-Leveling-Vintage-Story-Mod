namespace SeraphLeveling
{
    public class RemovalProgressData:ProgressData
    {
        public bool IsRemoved { get; set; }
        public RemovalProgressData()
        {
            IsRemoved = false;
        }
        public RemovalProgressData Clone()
        {
            return new RemovalProgressData
            {
                IsRemoved = this.IsRemoved
            };
        }
    }
}