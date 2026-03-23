using System;
using TEngine;
using UnityEngine;

namespace GameLogic.Regicide
{
    /// <summary>
    /// 角色动画驱动器：封装参数别名解析与状态触发，缺失时自动降级。
    /// </summary>
    internal sealed class RegicideActorAnimationDriver
    {
        private static readonly string[] AnimStateAliases = { "AnimState", "State", "AnimationState" };
        private static readonly string[] GroundedAliases = { "Grounded", "IsGrounded" };
        private static readonly string[] AttackAliases = { "Attack", "Atk", "DoAttack" };
        private static readonly string[] DamageAliases = { "Hurt", "Damage", "Hit", "TakeDamage" };
        private static readonly string[] DeathAliases = { "Death", "Dead", "Die" };
        private static readonly string[] RecoverAliases = { "Recover", "Reset", "BackToIdle" };

        private readonly Animator _animator;
        private readonly int _animStateHash;
        private readonly int _groundedHash;
        private readonly int _attackHash;
        private readonly int _damageHash;
        private readonly int _deathHash;
        private readonly int _recoverHash;
        private readonly bool _hasRecover;

        public string ActorName { get; }
        public bool IsReady { get; }

        private RegicideActorAnimationDriver(
            Animator animator,
            string actorName,
            bool isReady,
            int animStateHash,
            int groundedHash,
            int attackHash,
            int damageHash,
            int deathHash,
            int recoverHash,
            bool hasRecover)
        {
            _animator = animator;
            ActorName = actorName ?? string.Empty;
            IsReady = isReady;
            _animStateHash = animStateHash;
            _groundedHash = groundedHash;
            _attackHash = attackHash;
            _damageHash = damageHash;
            _deathHash = deathHash;
            _recoverHash = recoverHash;
            _hasRecover = hasRecover;
        }

        public static RegicideActorAnimationDriver Create(Animator animator, string actorName, string expectedControllerPath)
        {
            actorName = string.IsNullOrEmpty(actorName) ? "UnknownActor" : actorName;
            if (animator == null)
            {
                Log.Warning($"Regicide animation driver: {actorName} 缺少 Animator，已降级到位移表现。");
                return CreateFallback(actorName);
            }

            if (animator.runtimeAnimatorController == null)
            {
                Log.Warning($"Regicide animation driver: {actorName} 未绑定 AnimatorController（期望 {expectedControllerPath}），已降级到位移表现。");
                return CreateFallback(actorName);
            }

            bool okAnimState = TryResolveParam(animator, AnimatorControllerParameterType.Int, AnimStateAliases, out int animStateHash, out string animStateName);
            bool okGrounded = TryResolveParam(animator, AnimatorControllerParameterType.Bool, GroundedAliases, out int groundedHash, out string groundedName);
            bool okAttack = TryResolveParam(animator, AnimatorControllerParameterType.Trigger, AttackAliases, out int attackHash, out string attackName);
            bool okDamage = TryResolveParam(animator, AnimatorControllerParameterType.Trigger, DamageAliases, out int damageHash, out string damageName);
            bool okDeath = TryResolveParam(animator, AnimatorControllerParameterType.Trigger, DeathAliases, out int deathHash, out string deathName);
            bool hasRecover = TryResolveParam(animator, AnimatorControllerParameterType.Trigger, RecoverAliases, out int recoverHash, out string recoverName);

            if (!okAnimState || !okGrounded || !okAttack || !okDamage || !okDeath)
            {
                Log.Warning($"Regicide animation driver: {actorName} 参数不完整，已降级。required=[AnimState,Grounded,Attack,Damage/Hurt,Death]");
                return CreateFallback(actorName);
            }

            Log.Info($"Regicide animation driver ready: actor={actorName}, state={animStateName}, grounded={groundedName}, attack={attackName}, damage={damageName}, death={deathName}, recover={(hasRecover ? recoverName : "none")}");
            return new RegicideActorAnimationDriver(
                animator,
                actorName,
                isReady: true,
                animStateHash,
                groundedHash,
                attackHash,
                damageHash,
                deathHash,
                recoverHash,
                hasRecover);
        }

        public void SetIdle(int idleState)
        {
            if (!IsReady || _animator == null)
            {
                return;
            }

            _animator.ResetTrigger(_attackHash);
            _animator.ResetTrigger(_damageHash);
            _animator.SetBool(_groundedHash, true);
            _animator.SetInteger(_animStateHash, idleState);
            if (_hasRecover)
            {
                _animator.ResetTrigger(_recoverHash);
            }
        }

        public void TriggerAttack(int idleState)
        {
            if (!IsReady || _animator == null)
            {
                return;
            }

            _animator.SetBool(_groundedHash, true);
            _animator.SetInteger(_animStateHash, idleState);
            _animator.ResetTrigger(_damageHash);
            _animator.ResetTrigger(_deathHash);
            _animator.SetTrigger(_attackHash);
        }

        public void TriggerDamage(int idleState)
        {
            if (!IsReady || _animator == null)
            {
                return;
            }

            _animator.SetBool(_groundedHash, true);
            _animator.SetInteger(_animStateHash, idleState);
            _animator.ResetTrigger(_attackHash);
            _animator.ResetTrigger(_deathHash);
            _animator.SetTrigger(_damageHash);
        }

        public void TriggerDeath()
        {
            if (!IsReady || _animator == null)
            {
                return;
            }

            _animator.ResetTrigger(_attackHash);
            _animator.ResetTrigger(_damageHash);
            _animator.SetTrigger(_deathHash);
        }

        private static RegicideActorAnimationDriver CreateFallback(string actorName)
        {
            return new RegicideActorAnimationDriver(
                animator: null,
                actorName: actorName,
                isReady: false,
                animStateHash: 0,
                groundedHash: 0,
                attackHash: 0,
                damageHash: 0,
                deathHash: 0,
                recoverHash: 0,
                hasRecover: false);
        }

        private static bool TryResolveParam(
            Animator animator,
            AnimatorControllerParameterType expectedType,
            string[] aliases,
            out int paramHash,
            out string matchedName)
        {
            paramHash = 0;
            matchedName = string.Empty;
            if (animator == null || aliases == null || aliases.Length == 0)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            if (parameters == null || parameters.Length == 0)
            {
                return false;
            }

            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                string alias = aliases[aliasIndex];
                for (int i = 0; i < parameters.Length; i++)
                {
                    AnimatorControllerParameter parameter = parameters[i];
                    if (parameter.type != expectedType)
                    {
                        continue;
                    }

                    if (string.Equals(parameter.name, alias, StringComparison.OrdinalIgnoreCase))
                    {
                        paramHash = parameter.nameHash;
                        matchedName = parameter.name;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
