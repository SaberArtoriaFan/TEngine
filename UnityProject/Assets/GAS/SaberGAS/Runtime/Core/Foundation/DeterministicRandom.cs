using FastCloner.SourceGenerator.Shared;
using Herta;

namespace Saber.GAS.Foundation
{
    /// <summary>
    /// 轻量级确定性随机数生成器。
    /// 用于战斗仿真中的可重放随机逻辑。
    /// </summary>
    [FastClonerClonable]
    public struct DeterministicRandom
    {
        /// <summary>
        /// 缓存当前随机数内部状态。
        /// </summary>
        internal uint _state;

        /// <summary>
        /// 使用给定种子初始化随机数状态。
        /// </summary>
        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        /// <summary>
        /// 获取当前随机数内部状态。
        /// </summary>
        public uint State => _state;

        /// <summary>
        /// 生成下一个无符号整型随机值。
        /// </summary>
        public uint NextUInt()
        {
            var state = _state;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _state = state == 0 ? 1u : state;
            return _state;
        }

        /// <summary>
        /// 在指定整型区间内生成一个随机值。
        /// </summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            var range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        /// <summary>
        /// 在指定固定点区间内生成一个随机值。
        /// </summary>
        public FP NextFP(FP minInclusive, FP maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            var range = maxExclusive.RawValue - minInclusive.RawValue;
            if (range <= 0)
            {
                return minInclusive;
            }

            var offset = (long)(NextUInt() % (uint)range);
            var result = default(FP);
            result.RawValue = minInclusive.RawValue + offset;
            return result;
        }
    }
}
