using System.Collections.Generic;
using FastCloner.SourceGenerator.Shared;
using Herta;
using Saber.GAS.Foundation;

namespace Saber.GAS.Resources
{
    [FastClonerClonable]
    /// <summary>
    /// 一条资源消耗定义。
    /// </summary>
    public struct ResourceCost
    {
        /// <summary>
        /// 使用资源标识和数值创建消耗定义。
        /// </summary>
        public ResourceCost(ResourceId resourceId, FP amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        /// <summary>
        /// 获取消耗的资源 Id。
        /// </summary>
        public ResourceId ResourceId { get; }

        /// <summary>
        /// 获取消耗数值。
        /// </summary>
        public FP Amount { get; }
    }

    [FastClonerClonable]
    /// <summary>
    /// 单个资源的当前值、上限和回复速度。
    /// </summary>
    public sealed class ResourceValue
    {
        /// <summary>
        /// 创建一个全零的资源值。
        /// </summary>
        public ResourceValue()
            : this(FP._0, FP._0, FP._0)
        {
        }

        /// <summary>
        /// 使用当前值和上限创建资源值。
        /// </summary>
        public ResourceValue(FP current, FP max)
            : this(current, max, FP._0)
        {
        }

        /// <summary>
        /// 使用当前值、上限和每 Tick 回复量创建资源值。
        /// </summary>
        public ResourceValue(FP current, FP max, FP regenPerTick)
        {
            Current = current;
            Max = max;
            RegenPerTick = regenPerTick;
        }

        /// <summary>
        /// 获取当前资源值。
        /// </summary>
        public FP Current { get; internal set; }

        /// <summary>
        /// 获取资源上限。
        /// </summary>
        public FP Max { get; internal set; }

        /// <summary>
        /// 获取每 Tick 回复量。
        /// </summary>
        public FP RegenPerTick { get; internal set; }

        /// <summary>
        /// 写入当前值，并自动限制在合法区间内。
        /// </summary>
        public void SetCurrent(FP value)
        {
            Current = Clamp(value, FP._0, Max);
        }

        /// <summary>
        /// 写入最大值，并同步修正当前值。
        /// </summary>
        public void SetMax(FP value)
        {
            Max = value < FP._0 ? FP._0 : value;
            Current = Clamp(Current, FP._0, Max);
        }

        /// <summary>
        /// 写入每 Tick 回复量。
        /// </summary>
        public void SetRegenPerTick(FP value)
        {
            RegenPerTick = value;
        }

        /// <summary>
        /// 对当前值增减一个偏移量。
        /// </summary>
        public void Add(FP delta)
        {
            SetCurrent(Current + delta);
        }

        /// <summary>
        /// 判断当前资源是否足以支付指定消耗。
        /// </summary>
        public bool CanAfford(FP amount)
        {
            return Current >= amount;
        }

        /// <summary>
        /// 按每 Tick 回复量恢复资源。
        /// </summary>
        public void Regenerate()
        {
            if (RegenPerTick <= FP._0)
            {
                return;
            }

            Add(RegenPerTick);
        }

        /// <summary>
        /// 把数值限制在给定区间内。
        /// </summary>
        private static FP Clamp(FP value, FP min, FP max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        /// <summary>
        /// 清空资源状态，供对象池回收时复位。
        /// </summary>
        internal void Reset()
        {
            Current = FP._0;
            Max = FP._0;
            RegenPerTick = FP._0;
        }
    }

    [FastClonerClonable]
    /// <summary>
    /// 一组资源值的容器。
    /// </summary>
    public sealed class ResourceSet
    {
        /// <summary>
        /// 缓存全部资源值索引。
        /// </summary>
        private readonly Dictionary<ResourceId, ResourceValue> _values = new Dictionary<ResourceId, ResourceValue>();

        /// <summary>
        /// 获取内部资源字典。
        /// </summary>
        internal Dictionary<ResourceId, ResourceValue> Values => _values;

        /// <summary>
        /// 获取当前资源集合的全部条目。
        /// </summary>
        public IEnumerable<KeyValuePair<ResourceId, ResourceValue>> Entries => _values;

        /// <summary>
        /// 获取指定资源，不存在时按 0 创建。
        /// </summary>
        public ResourceValue GetOrCreate(ResourceId resourceId)
        {
            return GetOrCreate(resourceId, FP._0, FP._0);
        }

        /// <summary>
        /// 获取指定资源，不存在时按指定初值和上限创建。
        /// </summary>
        public ResourceValue GetOrCreate(ResourceId resourceId, FP defaultCurrent, FP defaultMax)
        {
            ResourceValue value;
            if (_values.TryGetValue(resourceId, out value))
            {
                return value;
            }

            value = new ResourceValue(defaultCurrent, defaultMax);
            _values[resourceId] = value;
            return value;
        }

        /// <summary>
        /// 判断当前资源集合是否能支付一组消耗。
        /// </summary>
        public bool CanAfford(IEnumerable<ResourceCost> costs)
        {
            if (costs == null)
            {
                return true;
            }

            foreach (var cost in costs)
            {
                var value = GetOrCreate(cost.ResourceId);
                if (!value.CanAfford(cost.Amount))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 扣除一组资源消耗。
        /// </summary>
        public void Spend(IEnumerable<ResourceCost> costs)
        {
            if (costs == null)
            {
                return;
            }

            foreach (var cost in costs)
            {
                GetOrCreate(cost.ResourceId).Add(-cost.Amount);
            }
        }

        /// <summary>
        /// 推进全部资源的一次自动回复。
        /// </summary>
        public void RegenerateAll()
        {
            foreach (var pair in _values)
            {
                pair.Value.Regenerate();
            }
        }

        /// <summary>
        /// 清空整个资源集合，供对象池回收时复位。
        /// </summary>
        internal void Reset()
        {
            foreach (var pair in _values)
            {
                pair.Value.Reset();
            }

            _values.Clear();
        }
    }
}
