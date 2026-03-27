using System;
using UnityEngine;

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// 供 Authoring/Editor 使用的全局资源定义条目。
    /// </summary>
    [Serializable]
    public sealed class CombatResourceDefinitionAuthoringData
    {
        [SerializeField]
        private string _resourceId;
        [SerializeField]
        private string _group;
        [SerializeField, TextArea(2, 3)]
        private string _note;

        public CombatResourceDefinitionAuthoringData()
        {
        }

        public CombatResourceDefinitionAuthoringData(string resourceId, string group, string note)
        {
            _resourceId = resourceId;
            _group = group;
            _note = note;
        }

        public string ResourceId => string.IsNullOrWhiteSpace(_resourceId) ? string.Empty : _resourceId.Trim();

        public string Group => string.IsNullOrWhiteSpace(_group) ? string.Empty : _group.Trim();

        public string Note => string.IsNullOrWhiteSpace(_note) ? string.Empty : _note.Trim();
    }
}
