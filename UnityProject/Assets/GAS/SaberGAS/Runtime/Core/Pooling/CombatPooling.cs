using System;
using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Projectiles;
using Saber.GAS.Triggers;

namespace Saber.GAS.Pooling
{
    /// <summary>
    /// 表示一个对象在归还池前可以自我清理运行时状态。
    /// </summary>
    public interface ICombatPoolable
    {
        void ResetForPool();
    }

    /// <summary>
    /// 面向引用类型的简单对象池。
    /// </summary>
    public sealed class CombatObjectPool<T>
        where T : class
    {
        /// <summary>
        /// 缓存对象创建工厂。
        /// </summary>
        private readonly Func<T> _factory;
        /// <summary>
        /// 缓存池中可复用对象栈。
        /// </summary>
        private readonly Stack<T> _items;

        /// <summary>
        /// 使用对象工厂和初始容量创建对象池。
        /// </summary>
        public CombatObjectPool(Func<T> factory, int initialCapacity = 0)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _factory = factory;
            _items = initialCapacity > 0
                ? new Stack<T>(initialCapacity)
                : new Stack<T>();
        }

        /// <summary>
        /// 获取池中当前缓存对象数量。
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 从池中取出对象；池为空时通过工厂创建。
        /// </summary>
        public T Rent()
        {
            return _items.Count > 0
                ? _items.Pop()
                : _factory();
        }

        /// <summary>
        /// 归还对象到池，并在必要时调用 ResetForPool。
        /// </summary>
        public void Return(T item)
        {
            if (item == null)
            {
                return;
            }

            var poolable = item as ICombatPoolable;
            if (poolable != null)
            {
                poolable.ResetForPool();
            }

            _items.Push(item);
        }

        /// <summary>
        /// 清空池中缓存对象。
        /// </summary>
        public void Reset()
        {
            _items.Clear();
        }
    }

    /// <summary>
    /// 专门复用 List 的轻量池。
    /// </summary>
    public sealed class CombatListPool<T>
    {
        /// <summary>
        /// 缓存池中可复用列表栈。
        /// </summary>
        private readonly Stack<List<T>> _items = new Stack<List<T>>();

        /// <summary>
        /// 获取池中当前缓存列表数量。
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 取出一个可写列表。
        /// </summary>
        public List<T> Rent()
        {
            return _items.Count > 0
                ? _items.Pop()
                : new List<T>();
        }

        /// <summary>
        /// 清空后归还列表。
        /// </summary>
        public void Return(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            list.Clear();
            _items.Push(list);
        }

        /// <summary>
        /// 清空列表池。
        /// </summary>
        public void Reset()
        {
            _items.Clear();
        }
    }

    /// <summary>
    /// CombatRuntime 统一持有的运行时对象池集合。
    /// </summary>
    public sealed class CombatRuntimePools
    {
        /// <summary>
        /// 创建 Runtime 使用的全部对象池。
        /// </summary>
        public CombatRuntimePools()
        {
            ActorStates = new CombatObjectPool<CombatActorState>(() => new CombatActorState());
            ActiveAbilityInstances = new CombatObjectPool<ActiveAbilityInstance>(() => new ActiveAbilityInstance());
            ActiveEffects = new CombatObjectPool<ActiveEffect>(() => new ActiveEffect());
            Projectiles = new CombatObjectPool<CombatProjectileState>(() => new CombatProjectileState());
            ActiveTriggers = new CombatObjectPool<ActiveTriggerInstance>(() => new ActiveTriggerInstance());
            AbilityTargetDataItems = new CombatObjectPool<AbilityTargetData>(() => new AbilityTargetData());
        }

        /// <summary>
        /// 获取战斗单位状态对象池。
        /// </summary>
        public CombatObjectPool<CombatActorState> ActorStates { get; }

        /// <summary>
        /// 获取技能实例对象池。
        /// </summary>
        public CombatObjectPool<ActiveAbilityInstance> ActiveAbilityInstances { get; }

        /// <summary>
        /// 获取持续效果对象池。
        /// </summary>
        public CombatObjectPool<ActiveEffect> ActiveEffects { get; }

        public CombatObjectPool<CombatProjectileState> Projectiles { get; }

        /// <summary>
        /// 获取 Trigger 实例对象池。
        /// </summary>
        public CombatObjectPool<ActiveTriggerInstance> ActiveTriggers { get; }

        /// <summary>
        /// 获取目标数据对象池。
        /// </summary>
        public CombatObjectPool<AbilityTargetData> AbilityTargetDataItems { get; }

        /// <summary>
        /// 重置所有运行时对象池。
        /// </summary>
        public void Reset()
        {
            ActorStates.Reset();
            ActiveAbilityInstances.Reset();
            ActiveEffects.Reset();
            Projectiles.Reset();
            ActiveTriggers.Reset();
            AbilityTargetDataItems.Reset();
        }
    }
}
