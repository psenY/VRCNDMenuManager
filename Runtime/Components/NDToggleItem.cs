using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Custom.NDMenuManager.Runtime
{
    [AddComponentMenu("ND Menu/ND Toggle Item")]
    [DisallowMultipleComponent]
    public class NDToggleItem : MonoBehaviour, INDMenuItem, IEditorOnly
    {
        [Header("Menu Display")]
        [SerializeField] private string menuName = "New Toggle";
        [SerializeField] private Texture2D icon;

        [Header("Parameter Settings")]
        [SerializeField] private string parameterName = "";
        [SerializeField] private bool defaultValue = false;
        [SerializeField] private bool saved = true;
        [SerializeField] private bool synced = true;

        [Header("Int Sync & Protection Mode (For Exclusive Outfits)")]
        [SerializeField] private bool useIntParameter = false;
        [SerializeField] private int parameterValue = 1;
        [SerializeField] private bool allowAllOff = false; // False = 防脱光保护 (只能在套装间切换)

        [Header("Targets (Off / On)")]
        public List<GameObjectToggleTarget> objectTargets = new List<GameObjectToggleTarget>();
        public List<BlendShapeToggleTarget> blendShapeTargets = new List<BlendShapeToggleTarget>();
        public List<MaterialPropertyToggleTarget> materialTargets = new List<MaterialPropertyToggleTarget>();

        // INDMenuItem implementation
        public string MenuName { get => menuName; set => menuName = value; }
        public Texture2D Icon { get => icon; set => icon = value; }
        public string ParameterName { get => parameterName; set => parameterName = value; }
        public bool DefaultValue { get => defaultValue; set => defaultValue = value; }
        public bool Saved { get => saved; set => saved = value; }
        public bool Synced { get => synced; set => synced = value; }
        public bool UseIntParameter { get => useIntParameter; set => useIntParameter = value; }
        public int ParameterValue { get => parameterValue; set => parameterValue = value; }
        public bool AllowAllOff { get => allowAllOff; set => allowAllOff = value; }

        public MenuItemType ItemType => MenuItemType.Toggle;

        public int GetBitCost()
        {
            if (!Synced) return 0;
            return UseIntParameter ? 8 : 1;
        }

        private void Reset()
        {
            menuName = gameObject.name;
            parameterName = "Toggle_" + gameObject.name.Replace(" ", "_");
        }
    }
}
