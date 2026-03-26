using System;

namespace Saber.GAS.Foundation
{
    /// <summary>
    /// 标识符基础校验工具。
    /// 用于保证字符串型 ID 在创建时就满足最基本的非空约束。
    /// </summary>
    internal static class IdentifierGuard
    {
        /// <summary>
        /// 校验传入的标识符字符串是否有效，并在无效时抛出异常。
        /// </summary>
        public static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identifier cannot be null or whitespace.", parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// 运行时角色标识。
    /// </summary>
    public struct ActorId : IEquatable<ActorId>
    {
        /// <summary>
        /// 表示空角色标识。
        /// </summary>
        public static readonly ActorId Empty = new ActorId(string.Empty, false);

        /// <summary>
        /// 使用字符串创建一个角色标识。
        /// </summary>
        public ActorId(string value)
            : this(value, true)
        {
        }

        /// <summary>
        /// 按需校验并创建角色标识。
        /// </summary>
        private ActorId(string value, bool validate)
        {
            Value = validate ? IdentifierGuard.Require(value, nameof(value)) : value ?? string.Empty;
        }

        /// <summary>
        /// 获取角色标识的原始字符串。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 判断当前标识是否为空。
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <summary>
        /// 比较两个角色标识是否相等。
        /// </summary>
        public bool Equals(ActorId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个角色标识。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ActorId other && Equals(other);
        }

        /// <summary>
        /// 计算角色标识的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回角色标识的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 比较两个角色标识是否相等。
        /// </summary>
        public static bool operator ==(ActorId left, ActorId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个角色标识是否不相等。
        /// </summary>
        public static bool operator !=(ActorId left, ActorId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 队伍标识。
    /// </summary>
    public struct TeamId : IEquatable<TeamId>
    {
        /// <summary>
        /// 表示空队伍标识。
        /// </summary>
        public static readonly TeamId Empty = new TeamId(string.Empty, false);

        /// <summary>
        /// 使用字符串创建一个队伍标识。
        /// </summary>
        public TeamId(string value)
            : this(value, true)
        {
        }

        /// <summary>
        /// 按需校验并创建队伍标识。
        /// </summary>
        private TeamId(string value, bool validate)
        {
            Value = validate ? IdentifierGuard.Require(value, nameof(value)) : value ?? string.Empty;
        }

        /// <summary>
        /// 获取队伍标识的原始字符串。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 判断当前标识是否为空。
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <summary>
        /// 比较两个队伍标识是否相等。
        /// </summary>
        public bool Equals(TeamId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个队伍标识。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is TeamId other && Equals(other);
        }

        /// <summary>
        /// 计算队伍标识的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回队伍标识的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 比较两个队伍标识是否相等。
        /// </summary>
        public static bool operator ==(TeamId left, TeamId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个队伍标识是否不相等。
        /// </summary>
        public static bool operator !=(TeamId left, TeamId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 技能定义标识。
    /// </summary>
    public struct AbilityId : IEquatable<AbilityId>
    {
        /// <summary>
        /// 表示空技能标识。
        /// </summary>
        public static readonly AbilityId Empty = new AbilityId(string.Empty, false);

        /// <summary>
        /// 使用字符串创建一个技能标识。
        /// </summary>
        public AbilityId(string value)
            : this(value, true)
        {
        }

        /// <summary>
        /// 按需校验并创建技能标识。
        /// </summary>
        private AbilityId(string value, bool validate)
        {
            Value = validate ? IdentifierGuard.Require(value, nameof(value)) : value ?? string.Empty;
        }

        /// <summary>
        /// 获取技能标识的原始字符串。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 判断当前标识是否为空。
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <summary>
        /// 比较两个技能标识是否相等。
        /// </summary>
        public bool Equals(AbilityId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个技能标识。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is AbilityId other && Equals(other);
        }

        /// <summary>
        /// 计算技能标识的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回技能标识的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 比较两个技能标识是否相等。
        /// </summary>
        public static bool operator ==(AbilityId left, AbilityId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个技能标识是否不相等。
        /// </summary>
        public static bool operator !=(AbilityId left, AbilityId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 运行时技能实例标识。
    /// </summary>
    public struct AbilityInstanceId : IEquatable<AbilityInstanceId>
    {
        /// <summary>
        /// 表示空技能实例标识。
        /// </summary>
        public static readonly AbilityInstanceId Empty = new AbilityInstanceId(0);

        /// <summary>
        /// 使用长整型值创建技能实例标识。
        /// </summary>
        public AbilityInstanceId(long value)
        {
            Value = value < 0 ? 0 : value;
        }

        /// <summary>
        /// 获取技能实例标识的原始数值。
        /// </summary>
        public long Value { get; }

        /// <summary>
        /// 判断当前标识是否为空。
        /// </summary>
        public bool IsEmpty => Value <= 0;

        /// <summary>
        /// 比较两个技能实例标识是否相等。
        /// </summary>
        public bool Equals(AbilityInstanceId other)
        {
            return Value == other.Value;
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个技能实例标识。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is AbilityInstanceId other && Equals(other);
        }

        /// <summary>
        /// 计算技能实例标识的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>
        /// 返回技能实例标识的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value.ToString();
        }

        /// <summary>
        /// 比较两个技能实例标识是否相等。
        /// </summary>
        public static bool operator ==(AbilityInstanceId left, AbilityInstanceId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个技能实例标识是否不相等。
        /// </summary>
        public static bool operator !=(AbilityInstanceId left, AbilityInstanceId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 效果定义标识。
    /// </summary>
    public struct EffectId : IEquatable<EffectId>
    {
        /// <summary>
        /// 表示空效果标识。
        /// </summary>
        public static readonly EffectId Empty = new EffectId(string.Empty, false);

        /// <summary>
        /// 使用字符串创建一个效果标识。
        /// </summary>
        public EffectId(string value)
            : this(value, true)
        {
        }

        /// <summary>
        /// 按需校验并创建效果标识。
        /// </summary>
        private EffectId(string value, bool validate)
        {
            Value = validate ? IdentifierGuard.Require(value, nameof(value)) : value ?? string.Empty;
        }

        /// <summary>
        /// 获取效果标识的原始字符串。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 判断当前标识是否为空。
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <summary>
        /// 比较两个效果标识是否相等。
        /// </summary>
        public bool Equals(EffectId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个效果标识。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is EffectId other && Equals(other);
        }

        /// <summary>
        /// 计算效果标识的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回效果标识的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 比较两个效果标识是否相等。
        /// </summary>
        public static bool operator ==(EffectId left, EffectId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个效果标识是否不相等。
        /// </summary>
        public static bool operator !=(EffectId left, EffectId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Trigger 定义标识。
    /// </summary>
    public struct TriggerId : IEquatable<TriggerId>
    {
        /// <summary>
        /// 表示空 Trigger 标识。
        /// </summary>
        public static readonly TriggerId Empty = new TriggerId(string.Empty, false);

        /// <summary>
        /// 使用字符串创建一个 Trigger 标识。
        /// </summary>
        public TriggerId(string value)
            : this(value, true)
        {
        }

        /// <summary>
        /// 按需校验并创建 Trigger 标识。
        /// </summary>
        private TriggerId(string value, bool validate)
        {
            Value = validate ? IdentifierGuard.Require(value, nameof(value)) : value ?? string.Empty;
        }

        /// <summary>
        /// 获取 Trigger 标识的原始字符串。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 判断当前标识是否为空。
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <summary>
        /// 比较两个 Trigger 标识是否相等。
        /// </summary>
        public bool Equals(TriggerId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个 Trigger 标识。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is TriggerId other && Equals(other);
        }

        /// <summary>
        /// 计算 Trigger 标识的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回 Trigger 标识的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 比较两个 Trigger 标识是否相等。
        /// </summary>
        public static bool operator ==(TriggerId left, TriggerId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个 Trigger 标识是否不相等。
        /// </summary>
        public static bool operator !=(TriggerId left, TriggerId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 属性标识。
    /// </summary>
    public struct AttributeId : IEquatable<AttributeId>
    {
        /// <summary>
        /// 表示空属性标识。
        /// </summary>
        public static readonly AttributeId Empty = new AttributeId(string.Empty, false);

        /// <summary>
        /// 使用字符串创建一个属性标识。
        /// </summary>
        public AttributeId(string value)
            : this(value, true)
        {
        }

        /// <summary>
        /// 按需校验并创建属性标识。
        /// </summary>
        private AttributeId(string value, bool validate)
        {
            Value = validate ? IdentifierGuard.Require(value, nameof(value)) : value ?? string.Empty;
        }

        /// <summary>
        /// 获取属性标识的原始字符串。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 判断当前标识是否为空。
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <summary>
        /// 比较两个属性标识是否相等。
        /// </summary>
        public bool Equals(AttributeId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个属性标识。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is AttributeId other && Equals(other);
        }

        /// <summary>
        /// 计算属性标识的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回属性标识的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 比较两个属性标识是否相等。
        /// </summary>
        public static bool operator ==(AttributeId left, AttributeId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个属性标识是否不相等。
        /// </summary>
        public static bool operator !=(AttributeId left, AttributeId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 资源标识。
    /// </summary>
    public struct ResourceId : IEquatable<ResourceId>
    {
        /// <summary>
        /// 表示空资源标识。
        /// </summary>
        public static readonly ResourceId Empty = new ResourceId(string.Empty, false);

        /// <summary>
        /// 使用字符串创建一个资源标识。
        /// </summary>
        public ResourceId(string value)
            : this(value, true)
        {
        }

        /// <summary>
        /// 按需校验并创建资源标识。
        /// </summary>
        private ResourceId(string value, bool validate)
        {
            Value = validate ? IdentifierGuard.Require(value, nameof(value)) : value ?? string.Empty;
        }

        /// <summary>
        /// 获取资源标识的原始字符串。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 判断当前标识是否为空。
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <summary>
        /// 比较两个资源标识是否相等。
        /// </summary>
        public bool Equals(ResourceId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 比较当前对象与另一个对象是否表示同一个资源标识。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ResourceId other && Equals(other);
        }

        /// <summary>
        /// 计算资源标识的哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回资源标识的字符串形式。
        /// </summary>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 比较两个资源标识是否相等。
        /// </summary>
        public static bool operator ==(ResourceId left, ResourceId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 比较两个资源标识是否不相等。
        /// </summary>
        public static bool operator !=(ResourceId left, ResourceId right)
        {
            return !left.Equals(right);
        }
    }
}
