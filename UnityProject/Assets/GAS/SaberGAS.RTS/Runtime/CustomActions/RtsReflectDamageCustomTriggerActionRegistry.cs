using System.Collections.Generic;
using Saber.GAS.Triggers;

namespace Saber.GAS.RTS.CustomActions
{
    /// <summary>
    /// RTS 反伤动作的回退注册表。
    /// 当源码生成注册不可用时，可通过 RuntimeOptions 手动注入。
    /// </summary>
    public sealed class RtsReflectDamageCustomTriggerActionRegistry : ICombatCustomTriggerActionRegistry
    {
        private static readonly int[] ActionIds = { RtsReflectDamageToFarthestEnemyTriggerAction.ActionId };
        private readonly ICombatCustomTriggerAction _action = new RtsReflectDamageToFarthestEnemyTriggerAction();

        /// <summary>
        /// 获取该注册表支持的动作 Id 列表。
        /// </summary>
        public IReadOnlyList<int> RegisteredCustomActionIds => ActionIds;

        /// <summary>
        /// 按动作 Id 解析实现。
        /// </summary>
        public bool TryResolve(int customActionId, out ICombatCustomTriggerAction action)
        {
            if (customActionId == RtsReflectDamageToFarthestEnemyTriggerAction.ActionId)
            {
                action = _action;
                return true;
            }

            action = null;
            return false;
        }
    }
}
