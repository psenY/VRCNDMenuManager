using System;
using UnityEngine;

namespace Custom.NDMenuManager.Runtime
{
    [Serializable]
    public class GameObjectToggleTarget
    {
        public GameObject targetObject;
        public bool activeWhenOn = true;
    }

    [Serializable]
    public class BlendShapeToggleTarget
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public string blendShapeName;
        [Range(0f, 100f)] public float valueWhenOff = 0f;
        [Range(0f, 100f)] public float valueWhenOn = 100f;
    }

    [Serializable]
    public class MaterialPropertyToggleTarget
    {
        public Renderer renderer;
        public int materialIndex = 0;
        public string propertyName;
        public float valueWhenOff = 0f;
        public float valueWhenOn = 1f;
    }

    [Serializable]
    public class BlendShapePuppetTarget
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public string blendShapeName;
        [Range(0f, 100f)] public float minValue = 0f;
        [Range(0f, 100f)] public float maxValue = 100f;
    }

    [Serializable]
    public class MaterialPuppetTarget
    {
        public Renderer renderer;
        public int materialIndex = 0;
        public string propertyName;
        public float minValue = 0f;
        public float maxValue = 1f;
    }
}
