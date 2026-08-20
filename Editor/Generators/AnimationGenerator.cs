using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Custom.NDMenuManager.Runtime;

namespace Custom.NDMenuManager.Editor.Generators
{
    public static class AnimationGenerator
    {
        public static string GetRelativePath(Transform root, Transform target)
        {
            if (target == null || root == null) return "";
            if (target == root) return "";

            var path = target.name;
            var parent = target.parent;
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        public static void CreateToggleClips(Transform avatarRoot, NDToggleItem toggle, out AnimationClip offClip, out AnimationClip onClip)
        {
            offClip = new AnimationClip { name = $"{toggle.MenuName}_Off" };
            onClip = new AnimationClip { name = $"{toggle.MenuName}_On" };

            // 1. GameObject active curves
            foreach (var target in toggle.objectTargets)
            {
                if (target.targetObject == null) continue;
                var relativePath = GetRelativePath(avatarRoot, target.targetObject.transform);

                float offValue = target.activeWhenOn ? 0f : 1f;
                float onValue = target.activeWhenOn ? 1f : 0f;

                SetConstantCurve(offClip, relativePath, typeof(GameObject), "m_IsActive", offValue);
                SetConstantCurve(onClip, relativePath, typeof(GameObject), "m_IsActive", onValue);
            }

            // 2. BlendShape curves
            foreach (var target in toggle.blendShapeTargets)
            {
                if (target.skinnedMeshRenderer == null || string.IsNullOrEmpty(target.blendShapeName)) continue;
                var relativePath = GetRelativePath(avatarRoot, target.skinnedMeshRenderer.transform);
                var property = $"blendShape.{target.blendShapeName}";

                SetConstantCurve(offClip, relativePath, typeof(SkinnedMeshRenderer), property, target.valueWhenOff);
                SetConstantCurve(onClip, relativePath, typeof(SkinnedMeshRenderer), property, target.valueWhenOn);
            }

            // 3. Material property curves
            foreach (var target in toggle.materialTargets)
            {
                if (target.renderer == null || string.IsNullOrEmpty(target.propertyName)) continue;
                var relativePath = GetRelativePath(avatarRoot, target.renderer.transform);
                var property = $"material.{target.propertyName}";

                SetConstantCurve(offClip, relativePath, target.renderer.GetType(), property, target.valueWhenOff);
                SetConstantCurve(onClip, relativePath, target.renderer.GetType(), property, target.valueWhenOn);
            }
        }

        public static void CreateSharedIntGroupClips(
            Transform avatarRoot,
            string groupName,
            List<NDToggleItem> toggles,
            out AnimationClip allOffClip,
            out List<(int val, string name, AnimationClip clip)> stateClips)
        {
            allOffClip = new AnimationClip { name = $"{groupName}_AllOff" };
            stateClips = new List<(int val, string name, AnimationClip clip)>();

            // 1. Collect union of all targets across all outfit toggles
            var allObjects = new HashSet<GameObject>();
            var allBlendShapes = new Dictionary<(Transform smrTransform, string shapeName), float>();
            var allMaterials = new Dictionary<(Transform rendTransform, string propName, Type rendType), float>();

            foreach (var toggle in toggles)
            {
                foreach (var obj in toggle.objectTargets)
                {
                    if (obj.targetObject != null) allObjects.Add(obj.targetObject);
                }
                foreach (var bs in toggle.blendShapeTargets)
                {
                    if (bs.skinnedMeshRenderer != null && !string.IsNullOrEmpty(bs.blendShapeName))
                    {
                        allBlendShapes[(bs.skinnedMeshRenderer.transform, bs.blendShapeName)] = bs.valueWhenOff;
                    }
                }
                foreach (var mat in toggle.materialTargets)
                {
                    if (mat.renderer != null && !string.IsNullOrEmpty(mat.propertyName))
                    {
                        allMaterials[(mat.renderer.transform, mat.propertyName, mat.renderer.GetType())] = mat.valueWhenOff;
                    }
                }
            }

            // 2. Build State 0 (All Off Clip -> turns off all clothes, reverts all BlendShapes)
            foreach (var go in allObjects)
            {
                var path = GetRelativePath(avatarRoot, go.transform);
                SetConstantCurve(allOffClip, path, typeof(GameObject), "m_IsActive", 0f);
            }
            foreach (var kvp in allBlendShapes)
            {
                var path = GetRelativePath(avatarRoot, kvp.Key.smrTransform);
                SetConstantCurve(allOffClip, path, typeof(SkinnedMeshRenderer), $"blendShape.{kvp.Key.shapeName}", kvp.Value);
            }
            foreach (var kvp in allMaterials)
            {
                var path = GetRelativePath(avatarRoot, kvp.Key.rendTransform);
                SetConstantCurve(allOffClip, path, kvp.Key.rendType, $"material.{kvp.Key.propName}", kvp.Value);
            }

            // 3. Build Active State Clip for each outfit toggle
            foreach (var activeToggle in toggles)
            {
                var clip = new AnimationClip { name = $"{groupName}_{activeToggle.MenuName}_On" };

                // GameObjects: Active toggle objects ON, other toggle objects OFF
                var activeObjDict = new Dictionary<GameObject, float>();
                foreach (var t in activeToggle.objectTargets)
                {
                    if (t.targetObject != null)
                    {
                        activeObjDict[t.targetObject] = t.activeWhenOn ? 1f : 0f;
                    }
                }

                foreach (var go in allObjects)
                {
                    var path = GetRelativePath(avatarRoot, go.transform);
                    float val = activeObjDict.TryGetValue(go, out float v) ? v : 0f;
                    SetConstantCurve(clip, path, typeof(GameObject), "m_IsActive", val);
                }

                // BlendShapes: Active toggle BlendShapes ON, other toggle BlendShapes reverted to OFF
                var activeBsDict = new Dictionary<(Transform, string), float>();
                foreach (var bs in activeToggle.blendShapeTargets)
                {
                    if (bs.skinnedMeshRenderer != null && !string.IsNullOrEmpty(bs.blendShapeName))
                    {
                        activeBsDict[(bs.skinnedMeshRenderer.transform, bs.blendShapeName)] = bs.valueWhenOn;
                    }
                }

                foreach (var kvp in allBlendShapes)
                {
                    var path = GetRelativePath(avatarRoot, kvp.Key.smrTransform);
                    float val = activeBsDict.TryGetValue(kvp.Key, out float v) ? v : kvp.Value;
                    SetConstantCurve(clip, path, typeof(SkinnedMeshRenderer), $"blendShape.{kvp.Key.shapeName}", val);
                }

                // Materials
                var activeMatDict = new Dictionary<(Transform, string, Type), float>();
                foreach (var mat in activeToggle.materialTargets)
                {
                    if (mat.renderer != null && !string.IsNullOrEmpty(mat.propertyName))
                    {
                        activeMatDict[(mat.renderer.transform, mat.propertyName, mat.renderer.GetType())] = mat.valueWhenOn;
                    }
                }

                foreach (var kvp in allMaterials)
                {
                    var path = GetRelativePath(avatarRoot, kvp.Key.rendTransform);
                    float val = activeMatDict.TryGetValue(kvp.Key, out float v) ? v : kvp.Value;
                    SetConstantCurve(clip, path, kvp.Key.rendType, $"material.{kvp.Key.propName}", val);
                }

                stateClips.Add((activeToggle.ParameterValue, activeToggle.MenuName, clip));
            }
        }

        public static List<AnimationClip> CreateToggleGroupClips(Transform avatarRoot, NDToggleGroup group)
        {
            var clips = new List<AnimationClip>();

            for (int i = 0; i < group.options.Count; i++)
            {
                var option = group.options[i];
                var clip = new AnimationClip { name = $"{group.MenuName}_Option_{i}_{option.optionName}" };

                for (int j = 0; j < group.options.Count; j++)
                {
                    var opt = group.options[j];
                    bool isCurrent = (i == j);

                    foreach (var target in opt.objectTargets)
                    {
                        if (target.targetObject == null) continue;
                        var relativePath = GetRelativePath(avatarRoot, target.targetObject.transform);
                        float val = isCurrent ? (target.activeWhenOn ? 1f : 0f) : (target.activeWhenOn ? 0f : 1f);
                        SetConstantCurve(clip, relativePath, typeof(GameObject), "m_IsActive", val);
                    }

                    foreach (var target in opt.blendShapeTargets)
                    {
                        if (target.skinnedMeshRenderer == null || string.IsNullOrEmpty(target.blendShapeName)) continue;
                        var relativePath = GetRelativePath(avatarRoot, target.skinnedMeshRenderer.transform);
                        var property = $"blendShape.{target.blendShapeName}";
                        float val = isCurrent ? target.valueWhenOn : target.valueWhenOff;
                        SetConstantCurve(clip, relativePath, typeof(SkinnedMeshRenderer), property, val);
                    }
                }

                clips.Add(clip);
            }

            return clips;
        }

        public static void CreateRadialPuppetClips(Transform avatarRoot, NDRadialPuppet puppet, out AnimationClip minClip, out AnimationClip maxClip)
        {
            minClip = new AnimationClip { name = $"{puppet.MenuName}_Min" };
            maxClip = new AnimationClip { name = $"{puppet.MenuName}_Max" };

            // BlendShape curves
            foreach (var target in puppet.blendShapeTargets)
            {
                if (target.skinnedMeshRenderer == null || string.IsNullOrEmpty(target.blendShapeName)) continue;
                var relativePath = GetRelativePath(avatarRoot, target.skinnedMeshRenderer.transform);
                var property = $"blendShape.{target.blendShapeName}";

                SetConstantCurve(minClip, relativePath, typeof(SkinnedMeshRenderer), property, target.minValue);
                SetConstantCurve(maxClip, relativePath, typeof(SkinnedMeshRenderer), property, target.maxValue);
            }

            // Material curves
            foreach (var target in puppet.materialTargets)
            {
                if (target.renderer == null || string.IsNullOrEmpty(target.propertyName)) continue;
                var relativePath = GetRelativePath(avatarRoot, target.renderer.transform);
                var property = $"material.{target.propertyName}";

                SetConstantCurve(minClip, relativePath, target.renderer.GetType(), property, target.minValue);
                SetConstantCurve(maxClip, relativePath, target.renderer.GetType(), property, target.maxValue);
            }
        }

        private static void SetConstantCurve(AnimationClip clip, string path, Type type, string propertyName, float value)
        {
            var curve = AnimationCurve.Constant(0f, 1f / 60f, value);
            clip.SetCurve(path, type, propertyName, curve);
        }
    }
}
