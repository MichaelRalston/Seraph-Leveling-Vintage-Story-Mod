namespace SeraphLeveling.Data
{

    /// <summary>
    /// Simple struct for tracking 2D positions without allocating Vec3d objects.
    /// Used in walking/sneaking tick handlers to avoid GC pressure.
    /// </summary>
    public struct Position2D
    {
        public double X;
        public double Z;

        public Position2D(double x, double z)
        {
            X = x;
            Z = z;
        }
    }
}
