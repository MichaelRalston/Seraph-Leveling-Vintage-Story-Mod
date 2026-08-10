namespace SeraphLeveling.Data
{
    public interface IDeepCopyable<T>
    {
        T Clone();
    }
}
