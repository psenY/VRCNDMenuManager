using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Custom.NDMenuManager.Runtime
{
    [Serializable]
    public class ToggleGroupOption
    {
        public string optionName = "Option";
        public Texture2D icon;
        public List<GameObjectToggleTarget> objectTargets = new List<GameObjectToggleTarget>();
        public List<BlendShapeToggleTarget> blendShapeTargets = new List<BlendShapeToggleTarget>();
    }

    [AddComponentMenu("ND Menu/ND Toggle Group")]
    [DisallowMultipleComponent]
    public class NDToggleGroup : MonoBehaviour, INDMenuItem, IEditorOnly
    {
        [Header("Menu Display")]
        [SerializeField] private string menuName = "New Outfit Group";
        [SerializeField] private Texture2D icon;

        [Header("Parameter Settings")]
        [SerializeField] private string parameterName = "";
        [SerializeField] private int defaultIndex = 0;
        [SerializeField] private bool saved = true;
        [SerializeField] private bool synced = true;
        [SerializeField] private bool allowAllOff = false;

        [Header("Options")]
        public List<ToggleGroupOption> options = new List<ToggleGroupOption>();

        // INDMenuItem implementation
        public string MenuName { get => menuName; set => menuName = value; }
        public Texture2D Icon { get => icon; set => icon = value; }
        public string ParameterName { get => parameterName; set => parameterName = value; }
        public int DefaultIndex { get => defaultIndex; set => defaultIndex = value; }
        public bool Saved { get => saved; set => saved = value; }
        public bool Synced { get => synced; set => synced = value; }
        public bool AllowAllOff { get => allowAllOff; set => allowAllOff = value; }
        public MenuItemType ItemType => MenuItemType.ToggleGroup;

        public int GetBitCost() => Synced ? 8 : 0;

        private void Reset()
        {
            menuName = gameObject.name;
            parameterName = "Group_" + gameObject.name.Replace(" ", "_");
        }
    }
}
