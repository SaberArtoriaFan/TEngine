using System;

namespace GameProto.Regicide
{
    public static class RegicideSeedUtility
    {
        public static int GenerateSessionSeed()
        {
            long unixMilliseconds = RegicideClock.NowUnixMilliseconds();
            int timePart = (int)(unixMilliseconds ^ (unixMilliseconds >> 32));
            int entropyPart = Guid.NewGuid().GetHashCode();
            int seed = timePart ^ entropyPart;
            if (seed == int.MinValue)
            {
                seed = int.MaxValue;
            }

            seed = Math.Abs(seed);
            return seed == 0 ? 1 : seed;
        }
    }
}
