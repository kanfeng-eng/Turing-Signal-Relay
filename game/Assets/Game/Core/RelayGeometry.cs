namespace TuringSignalRelay
{
    public static class RelayGeometry
    {
        public static bool Within(int ax, int ay, int bx, int by, int radius)
        {
            if (radius < 0) return false;
            long x = (long)ax - bx, y = (long)ay - by;
            if (System.Math.Abs(x) > radius || System.Math.Abs(y) > radius) return false;
            return x * x + y * y <= (long)radius * radius;
        }
    }
}
