using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Custom.NDMenuManager.Runtime
{
    [AddComponentMenu("ND Menu/ND SubMenu")]
    [DisallowMultipleComponent]
    public class NDSubMenu : MonoBehaviour, INDMenuItem, IEditorOnly
    {
        [Header("Menu Display")]
        [SerializeField] private string menuName = "New SubMenu";
        [SerializeField] private Texture2D icon;

        [Header("Child Menu Items (Optional - will auto-scan children if empty)")]
        public List<MonoBehaviour> explicitMenuItems = new List<MonoBehaviour>();

        // INDMenuItem implementation
        public string MenuName { get => menuName; set => menuName = value; }
        public Texture2D Icon { get => icon; set => icon = value; }
        public string ParameterName { get => ""; set { } }
        public bool Saved { get => false; set { } }
        public bool Synced { get => false; set { } }
        public MenuItemType ItemType => MenuItemType.SubMenu;

        public int GetBitCost() => 0; // Submenus themselves don't consume parameter bits

        private void Reset()
        {
            menuName = gameObject.name;
        }
    }
}
