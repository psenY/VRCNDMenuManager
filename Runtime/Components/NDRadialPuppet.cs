using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Custom.NDMenuManager.Runtime
{
    [AddComponentMenu("ND Menu/ND Radial Puppet")]
    [DisallowMultipleComponent]
    public class NDRadialPuppet : MonoBehaviour, INDMenuItem, IEditorOnly
    {
        [Header("Menu Display")]
        [SerializeField] private string menuName = "New Radial Slider";
        [SerializeField] private Texture2D icon;

        [Header("Parameter Settings")]
        [SerializeField] private string parameterName = "";
        [SerializeField] [Range(0f, 1f)] private float defaultValue = 0f;
        [SerializeField] private bool saved = true;
        [SerializeField] private bool synced = true;

        [Header("Puppet Targets (0.0 to 1.0)")]
        public List<BlendShapePuppetTarget> blendShapeTargets = new List<BlendShapePuppetTarget>();
        public List<MaterialPuppetTarget> materialTargets = new List<MaterialPuppetTarget>();

        // INDMenuItem implementation
        public string MenuName { get => menuName; set => menuName = value; }
        public Texture2D Icon { get => icon; set => icon = value; }
        public string ParameterName { get => parameterName; set => parameterName = value; }
        public float DefaultValue { get => defaultValue; set => defaultValue = value; }
        public bool Saved { get => saved; set => saved = value; }
        public bool Synced { get => synced; set => synced = value; }
        public MenuItemType ItemType => MenuItemType.RadialPuppet;

        public int GetBitCost() => Synced ? 8 : 0;

        private void Reset()
        {
            menuName = gameObject.name;
            parameterName = "Radial_" + gameObject.name.Replace(" ", "_");
        }
    }
}
