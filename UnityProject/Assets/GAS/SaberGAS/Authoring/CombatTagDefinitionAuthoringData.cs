using System;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    [Flags]
    public enum CombatTagUsage
    {
        None = 0,
        Shared = 1 << 0,
        Actor = 1 << 1,
        Ability = 1 << 2,
        Effect = 1 << 3,
        Trigger = 1 << 4,
        Impact = 1 << 5,
        All = Shared | Actor | Ability | Effect | Trigger | Impact,
    }

    /// <summary>
    /// 供 Authoring/Editor 使用的标签定义条目。
    /// </summary>
    [Serializable]
    public sealed class CombatTagDefinitionAuthoringData
    {
        [SerializeField]
        private string _tag;
        [SerializeField]
        private string _group;
        [SerializeField]
        private CombatTagUsage _usage = CombatTagUsage.Shared;
        [SerializeField, TextArea(2, 3)]
        private string _note;

        public CombatTagDefinitionAuthoringData()
        {
        }

        public CombatTagDefinitionAuthoringData(string tag, string group, CombatTagUsage usage, string note)
        {
            _tag = tag;
            _group = group;
            _usage = usage;
            _note = note;
        }

        public string Tag => string.IsNullOrWhiteSpace(_tag) ? string.Empty : _tag.Trim();

        public string Group => string.IsNullOrWhiteSpace(_group) ? string.Empty : _group.Trim();

        public CombatTagUsage Usage => _usage == CombatTagUsage.None ? CombatTagUsage.Shared : _usage;

        public string Note => string.IsNullOrWhiteSpace(_note) ? string.Empty : _note.Trim();
    }
}
