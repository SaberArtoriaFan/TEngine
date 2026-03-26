using System;
using System.Collections.Generic;
using Herta;
using Saber.GAS.Foundation;

namespace Saber.GAS.Attributes
{
    /// <summary>
    /// 属性修正的计算方式。
    /// </summary>
    public enum AttributeModifierType
    {
        /// <summary>
        /// 直接加到基础值上。
        /// </summary>
        Add = 0,
        /// <summary>
        /// 以乘法系数参与最终计算。
        /// </summary>
        Multiply = 1,
        /// <summary>
        /// 直接覆盖最终结果。
        /// </summary>
        Override = 2,
    }

    /// <summary>
    /// 单条属性修正记录。
    /// </summary>
    public sealed class AttributeModifier
    {
        /// <summary>
        /// 创建供池化和深拷贝使用的空修正对象。
        /// </summary>
        internal AttributeModifier()
        {
        }

        /// <summary>
        /// 使用属性、修正类型、数值和来源构造修正记录。
        /// </summary>
        public AttributeModifier(AttributeId attributeId, AttributeModifierType modifierType, FP magnitude, object source)
        {
            AttributeId = attributeId;
            ModifierType = modifierType;
            Magnitude = magnitude;
            Source = source;
        }

        /// <summary>
        /// 获取修正作用的属性 Id。
        /// </summary>
        public AttributeId AttributeId { get; internal set; }

        /// <summary>
        /// 获取修正计算方式。
        /// </summary>
        public AttributeModifierType ModifierType { get; internal set; }

        /// <summary>
        /// 获取修正数值。
        /// </summary>
        public FP Magnitude { get; internal set; }

        /// <summary>
        /// 获取修正来源对象。
        /// </summary>
        public object Source { get; internal set; }
    }

    /// <summary>
    /// 单个属性的基础值与修正列表。
    /// </summary>
    public sealed class AttributeValue
    {
        /// <summary>
        /// 缓存当前属性的修正列表。
        /// </summary>
        private readonly List<AttributeModifier> _modifiers = new List<AttributeModifier>();

        /// <summary>
        /// 创建一个基础值为 0 的属性值。
        /// </summary>
        public AttributeValue()
            : this(FP._0)
        {
        }

        /// <summary>
        /// 使用指定基础值创建属性值。
        /// </summary>
        public AttributeValue(FP baseValue)
        {
            BaseValue = baseValue;
        }

        /// <summary>
        /// 获取内部可写修正列表。
        /// </summary>
        internal List<AttributeModifier> ModifierList => _modifiers;

        /// <summary>
        /// 获取属性基础值。
        /// </summary>
        public FP BaseValue { get; internal set; }

        /// <summary>
        /// 获取只读修正列表。
        /// </summary>
        public IReadOnlyList<AttributeModifier> Modifiers => _modifiers;

        /// <summary>
        /// 修改属性的基础值。
        /// </summary>
        public void SetBaseValue(FP value)
        {
            BaseValue = value;
        }

        /// <summary>
        /// 向属性追加一条修正记录。
        /// </summary>
        public void AddModifier(AttributeModifier modifier)
        {
            _modifiers.Add(modifier);
        }

        /// <summary>
        /// 移除某个来源挂上的全部修正。
        /// </summary>
        public int RemoveModifiersBySource(object source)
        {
            if (source == null)
            {
                return 0;
            }

            return _modifiers.RemoveAll(modifier => ReferenceEquals(modifier.Source, source) || Equals(modifier.Source, source));
        }

        /// <summary>
        /// 计算当前属性的最终值。
        /// </summary>
        public FP Evaluate()
        {
            var add = FP._0;
            var multiply = FP._1;
            var hasOverride = false;
            var overrideValue = FP._0;

            for (var i = 0; i < _modifiers.Count; i++)
            {
                var modifier = _modifiers[i];
                switch (modifier.ModifierType)
                {
                    case AttributeModifierType.Add:
                        add += modifier.Magnitude;
                        break;
                    case AttributeModifierType.Multiply:
                        multiply *= modifier.Magnitude;
                        break;
                    case AttributeModifierType.Override:
                        hasOverride = true;
                        overrideValue = modifier.Magnitude;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (hasOverride)
            {
                return overrideValue;
            }

            return (BaseValue + add) * multiply;
        }

        /// <summary>
        /// 清空属性值，供对象池回收时复位。
        /// </summary>
        internal void Reset()
        {
            BaseValue = FP._0;
            _modifiers.Clear();
        }
    }

    /// <summary>
    /// 一组属性值的集合容器。
    /// </summary>
    public sealed class AttributeSet
    {
        /// <summary>
        /// 缓存全部属性值索引。
        /// </summary>
        private readonly Dictionary<AttributeId, AttributeValue> _values = new Dictionary<AttributeId, AttributeValue>();

        /// <summary>
        /// 获取内部属性值字典。
        /// </summary>
        internal Dictionary<AttributeId, AttributeValue> Values => _values;

        /// <summary>
        /// 获取当前属性集合的全部条目。
        /// </summary>
        public IEnumerable<KeyValuePair<AttributeId, AttributeValue>> Entries => _values;

        /// <summary>
        /// 判断集合中是否已经存在指定属性。
        /// </summary>
        public bool Has(AttributeId attributeId)
        {
            return _values.ContainsKey(attributeId);
        }

        /// <summary>
        /// 获取指定属性，不存在时以 0 为基础值创建。
        /// </summary>
        public AttributeValue GetOrCreate(AttributeId attributeId)
        {
            return GetOrCreate(attributeId, FP._0);
        }

        /// <summary>
        /// 获取指定属性，不存在时按给定基础值创建。
        /// </summary>
        public AttributeValue GetOrCreate(AttributeId attributeId, FP defaultBaseValue)
        {
            AttributeValue value;
            if (_values.TryGetValue(attributeId, out value))
            {
                return value;
            }

            value = new AttributeValue(defaultBaseValue);
            _values[attributeId] = value;
            return value;
        }

        /// <summary>
        /// 获取属性的当前计算结果，缺失时按 0 处理。
        /// </summary>
        public FP GetCurrent(AttributeId attributeId)
        {
            return GetCurrent(attributeId, FP._0);
        }

        /// <summary>
        /// 获取属性的当前计算结果，缺失时按指定基础值创建。
        /// </summary>
        public FP GetCurrent(AttributeId attributeId, FP defaultBaseValue)
        {
            return GetOrCreate(attributeId, defaultBaseValue).Evaluate();
        }

        /// <summary>
        /// 获取属性的基础值，缺失时按 0 处理。
        /// </summary>
        public FP GetBase(AttributeId attributeId)
        {
            return GetBase(attributeId, FP._0);
        }

        /// <summary>
        /// 获取属性的基础值，缺失时按指定基础值创建。
        /// </summary>
        public FP GetBase(AttributeId attributeId, FP defaultBaseValue)
        {
            return GetOrCreate(attributeId, defaultBaseValue).BaseValue;
        }

        /// <summary>
        /// 直接写入属性基础值。
        /// </summary>
        public void SetBase(AttributeId attributeId, FP baseValue)
        {
            GetOrCreate(attributeId).SetBaseValue(baseValue);
        }

        /// <summary>
        /// 将一条修正添加到对应属性上。
        /// </summary>
        public void AddModifier(AttributeModifier modifier)
        {
            GetOrCreate(modifier.AttributeId).AddModifier(modifier);
        }

        /// <summary>
        /// 从所有属性里移除指定来源的修正。
        /// </summary>
        public int RemoveModifiersBySource(object source)
        {
            var removed = 0;

            foreach (var pair in _values)
            {
                removed += pair.Value.RemoveModifiersBySource(source);
            }

            return removed;
        }

        /// <summary>
        /// 清空整个属性集合，供对象池回收时复位。
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
