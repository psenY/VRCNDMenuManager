using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Custom.NDMenuManager.Runtime
{
    [AddComponentMenu("ND Menu/ND Menu Root")]
    [DisallowMultipleComponent]
    public class NDMenuRoot : MonoBehaviour, IEditorOnly
    {
        [Header("Menu Mode")]
        [Tooltip("若勾选，构建时将完全用 ND 菜单覆盖原模型菜单；若不勾选，则作为扩展项追加在原菜单中")]
        public bool overrideOriginalMenu = false;

        [Header("Global Menu Settings")]
        public string rootMenuPrefix = "";
        public bool autoSplitLargeMenus = true; // Auto split when > 8 controls

        [Tooltip("Direct top-level menu items. If empty, all items under this GameObject hierarchy will be organized.")]
        public List<MonoBehaviour> rootItems = new List<MonoBehaviour>();
    }
}
