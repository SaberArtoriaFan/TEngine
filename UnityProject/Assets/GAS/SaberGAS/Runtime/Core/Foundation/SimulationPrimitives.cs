using System;
using Herta;

namespace Saber.GAS.Foundation
{
    /// <summary>
    /// 仿真 Tick 值。
    /// 用于表示整个战斗世界里的离散时间步。
    /// </summary>
    public struct SimulationTick : IEquatable<SimulationTick>, IComparable<SimulationTick>
    {
        /// <summary>
        /// 表示起始 Tick。
        /// </summary>
        public static readonly SimulationTick Zero = new SimulationTick(0);

        /// <summary>
        /// 使用长整型值创建一个 Tick。
        /// 负数会被收敛为 0。
        /// </summary>
        public SimulationTick(long value)
        {
            Value = value < 0 ? 0 : value;
        }

        /// <summary>
        /// 获取 Tick 的原始数值。
        /// </summary>
        public long Value { get; }

        /// <summary>
        /// 生成当前 Tick 的下一个 Tick。
        /// </summary>
        public SimulationTick Next()
        {
            return new SimulationTick(Value + 1);
        }

        /// <summary>
        /// 比较两个 Tick 的先后顺序。
        /// </summary>
        public int CompareTo(SimulationTick other)
        {
            return Value.CompareTo(other.Value);
        }

        /// <summary>
        /// 比较两个 Tick 是否相等。
        /// </summary>
        public bool Equals(SimulationTick other)
        {
            return Value == other.Value;
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个 Tick。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is SimulationTick other && Equals(other);
        }

        /// <summary>
        /// 计算 Tick 的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>
        /// 返回 Tick 的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value.ToString();
        }

        /// <summary>
        /// 对 Tick 增加一个偏移量。
        /// </summary>
        public static SimulationTick operator +(SimulationTick left, long delta)
        {
            return new SimulationTick(left.Value + delta);
        }

        /// <summary>
        /// 计算两个 Tick 之间的差值。
        /// </summary>
        public static long operator -(SimulationTick left, SimulationTick right)
        {
            return left.Value - right.Value;
        }

        /// <summary>
        /// 判断左侧 Tick 是否小于右侧 Tick。
        /// </summary>
        public static bool operator <(SimulationTick left, SimulationTick right)
        {
            return left.Value < right.Value;
        }

        /// <summary>
        /// 判断左侧 Tick 是否大于右侧 Tick。
        /// </summary>
        public static bool operator >(SimulationTick left, SimulationTick right)
        {
            return left.Value > right.Value;
        }

        /// <summary>
        /// 判断左侧 Tick 是否小于等于右侧 Tick。
        /// </summary>
        public static bool operator <=(SimulationTick left, SimulationTick right)
        {
            return left.Value <= right.Value;
        }

        /// <summary>
        /// 判断左侧 Tick 是否大于等于右侧 Tick。
        /// </summary>
        public static bool operator >=(SimulationTick left, SimulationTick right)
        {
            return left.Value >= right.Value;
        }
    }

    /// <summary>
    /// 三维世界中的固定点坐标。
    /// </summary>
    public struct WorldPosition : IEquatable<WorldPosition>
    {
        /// <summary>
        /// 表示原点坐标。
        /// </summary>
        public static readonly WorldPosition Zero = new WorldPosition(FP._0, FP._0, FP._0);

        /// <summary>
        /// 使用三个固定点值创建一个世界坐标。
        /// </summary>
        public WorldPosition(FP x, FP y, FP z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 获取 X 轴坐标。
        /// </summary>
        public FP X { get; }

        /// <summary>
        /// 获取 Y 轴坐标。
        /// </summary>
        public FP Y { get; }

        /// <summary>
        /// 获取 Z 轴坐标。
        /// </summary>
        public FP Z { get; }

        /// <summary>
        /// 计算两个世界坐标之间的平方距离。
        /// </summary>
        public static FP DistanceSquared(WorldPosition left, WorldPosition right)
        {
            var dx = left.X - right.X;
            var dy = left.Y - right.Y;
            var dz = left.Z - right.Z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        /// <summary>
        /// 比较两个世界坐标是否相等。
        /// </summary>
        public bool Equals(WorldPosition other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个世界坐标。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is WorldPosition other && Equals(other);
        }

        /// <summary>
        /// 计算世界坐标的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Z.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// 返回世界坐标的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return string.Format("({0}, {1}, {2})", X, Y, Z);
        }
    }
}
