using System;
using System.Collections;
using System.Collections.Generic;
using FastCloner.SourceGenerator.Shared;

namespace Saber.GAS.Tags
{
    /// <summary>
    /// 单个 GameplayTag 值对象。
    /// </summary>
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        /// <summary>
        /// 使用字符串创建一个 GameplayTag。
        /// </summary>
        public GameplayTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Tag cannot be null or whitespace.", nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// 获取标签原始字符串。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 比较两个标签是否相等。
        /// </summary>
        public bool Equals(GameplayTag other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个标签。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is GameplayTag other && Equals(other);
        }

        /// <summary>
        /// 计算标签的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回标签的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 比较两个标签是否相等。
        /// </summary>
        public static bool operator ==(GameplayTag left, GameplayTag right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个标签是否不相等。
        /// </summary>
        public static bool operator !=(GameplayTag left, GameplayTag right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// GameplayTag 集合容器。
    /// 用于完成包含、交集和批量添加等基础标签操作。
    /// </summary>
    [FastClonerClonable]
    public sealed class GameplayTagContainer : IEnumerable<GameplayTag>
    {
        /// <summary>
        /// 实际存储 Tag 的去重集合。
        /// </summary>
        private readonly HashSet<GameplayTag> _tags = new HashSet<GameplayTag>();

        /// <summary>
        /// 获取内部 HashSet 存储。
        /// </summary>
        internal HashSet<GameplayTag> TagsSet => _tags;

        /// <summary>
        /// 创建一个空标签容器。
        /// </summary>
        public GameplayTagContainer()
        {
        }

        /// <summary>
        /// 使用已有标签序列创建一个标签容器。
        /// </summary>
        public GameplayTagContainer(IEnumerable<GameplayTag> tags)
        {
            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags)
            {
                _tags.Add(tag);
            }
        }

        /// <summary>
        /// 获取当前容器中的标签数量。
        /// </summary>
        public int Count => _tags.Count;

        /// <summary>
        /// 向容器中添加一个标签。
        /// </summary>
        public void Add(GameplayTag tag)
        {
            _tags.Add(tag);
        }

        /// <summary>
        /// 向容器中批量添加标签。
        /// </summary>
        public void AddRange(IEnumerable<GameplayTag> tags)
        {
            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags)
            {
                _tags.Add(tag);
            }
        }

        /// <summary>
        /// 判断容器中是否包含指定标签。
        /// </summary>
        public bool Contains(GameplayTag tag)
        {
            return _tags.Contains(tag);
        }

        /// <summary>
        /// 判断容器中是否包含给定序列里的全部标签。
        /// </summary>
        public bool ContainsAll(IEnumerable<GameplayTag> tags)
        {
            if (tags == null)
            {
                return true;
            }

            foreach (var tag in tags)
            {
                if (!_tags.Contains(tag))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 判断容器中是否包含给定序列里的任意标签。
        /// </summary>
        public bool ContainsAny(IEnumerable<GameplayTag> tags)
        {
            if (tags == null)
            {
                return false;
            }

            foreach (var tag in tags)
            {
                if (_tags.Contains(tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从容器中移除一个标签。
        /// </summary>
        public bool Remove(GameplayTag tag)
        {
            return _tags.Remove(tag);
        }

        /// <summary>
        /// 清空容器中的所有标签。
        /// </summary>
        public void Clear()
        {
            _tags.Clear();
        }

        /// <summary>
        /// 获取标签容器的枚举器。
        /// </summary>
        public IEnumerator<GameplayTag> GetEnumerator()
        {
            return _tags.GetEnumerator();
        }

        /// <summary>
        /// 获取非泛型枚举器。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
