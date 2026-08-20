using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Custom.NDMenuManager.Runtime;
using Custom.NDMenuManager.Editor.Generators;

namespace Custom.NDMenuManager.Editor.NDMF
{
    public static class NDMenuBuildPass
    {
        public static void Execute(BuildContext context)
        {
            var avatarRoot = context.AvatarRootObject;
            if (avatarRoot == null) return;

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return;

            // Find all modular ND components on clothes/props
            var toggleItems = avatarRoot.GetComponentsInChildren<NDToggleItem>(true);
            var toggleGroups = avatarRoot.GetComponentsInChildren<NDToggleGroup>(true);
            var radialPuppets = avatarRoot.GetComponentsInChildren<NDRadialPuppet>(true);
            var subMenus = avatarRoot.GetComponentsInChildren<NDSubMenu>(true);
            var menuRoots = avatarRoot.GetComponentsInChildren<NDMenuRoot>(true);

            int totalComponents = toggleItems.Length + toggleGroups.Length + radialPuppets.Length + subMenus.Length;
            if (totalComponents == 0) return;

            // 1. Ensure FX Animator Controller exists (cloned non-destructively)
            var fxController = EnsureFXAnimatorController(descriptor);

            // 2. Ensure Expressions Menu & Parameters exist (cloned non-destructively to preserve base/MA systems)
            EnsureExpressionsAssets(descriptor);
            var expressionsMenu = descriptor.expressionsMenu;
            var expressionParameters = descriptor.expressionParameters;

            // 3. Map submenus hierarchically from root to leaves (supports nested folders like 衣柜 -> 套装1)
            var subMenuMap = new Dictionary<NDSubMenu, VRCExpressionsMenu>();
            var orderedSubMenus = subMenus.OrderBy(sm => GetHierarchyDepth(sm.transform)).ToList();
            foreach (var sm in orderedSubMenus)
            {
                var parentMenu = FindParentMenu(sm.transform, subMenuMap, expressionsMenu);
                var vrcSubMenu = MenuMerger.CreateSubMenuControl(parentMenu, sm.MenuName, sm.Icon);
                subMenuMap[sm] = vrcSubMenu;
            }

            // 4. Process Toggle Items
            var standardToggles = new List<NDToggleItem>();
            var intToggleGroups = new Dictionary<string, List<NDToggleItem>>();

            foreach (var toggle in toggleItems)
            {
                if (toggle == null) continue;
                if (toggle.UseIntParameter)
                {
                    string pName = string.IsNullOrEmpty(toggle.ParameterName) ? "Wardrobe_Select" : toggle.ParameterName;
                    if (!intToggleGroups.ContainsKey(pName))
                    {
                        intToggleGroups[pName] = new List<NDToggleItem>();
                    }
                    intToggleGroups[pName].Add(toggle);
                }
                else
                {
                    standardToggles.Add(toggle);
                }
            }

            // 4. Process Shared Int Toggles FIRST (e.g. Wardrobe exclusive main outfit switches -> Slot 1)
            foreach (var kvp in intToggleGroups)
            {
                string paramName = kvp.Key;
                var groupToggles = kvp.Value;

                int defaultVal = 0;
                bool saved = true;
                bool synced = true;

                foreach (var toggle in groupToggles)
                {
                    if (toggle.DefaultValue) defaultVal = toggle.ParameterValue;
                    saved = toggle.Saved;
                    synced = toggle.Synced;

                    var targetMenu = FindParentMenu(toggle.transform, subMenuMap, expressionsMenu);
                    MenuMerger.AddToggleControl(targetMenu, toggle, true);
                }

                AnimationGenerator.CreateSharedIntGroupClips(
                    avatarRoot.transform,
                    paramName,
                    groupToggles,
                    out var allOffClip,
                    out var states
                );

                bool allowAllOff = groupToggles.Exists(t => t.AllowAllOff);
                MenuMerger.AddParameter(expressionParameters, paramName, VRCExpressionParameters.ValueType.Int, defaultVal, saved, synced);
                ControllerGenerator.AddSharedIntToggleLayer(fxController, paramName, defaultVal, allOffClip, states, allowAllOff);
            }

            // 5. Process Standard Bool Toggles SECOND (e.g. Sub-accessories like Jacket, Hat, Socks)
            foreach (var toggle in standardToggles)
            {
                AnimationGenerator.CreateToggleClips(avatarRoot.transform, toggle, out var offClip, out var onClip);
                ControllerGenerator.AddToggleLayer(fxController, toggle, offClip, onClip);

                string paramName = string.IsNullOrEmpty(toggle.ParameterName) ? toggle.MenuName : toggle.ParameterName;
                MenuMerger.AddParameter(expressionParameters, paramName, VRCExpressionParameters.ValueType.Bool, toggle.DefaultValue ? 1f : 0f, toggle.Saved, toggle.Synced);

                var targetMenu = FindParentMenu(toggle.transform, subMenuMap, expressionsMenu);
                MenuMerger.AddToggleControl(targetMenu, toggle);
            }

            // 5. Process Toggle Groups (Radio outfit groups)
            foreach (var group in toggleGroups)
            {
                if (group == null) continue;

                var optionClips = AnimationGenerator.CreateToggleGroupClips(avatarRoot.transform, group);
                ControllerGenerator.AddToggleGroupLayer(fxController, group, optionClips);

                string paramName = string.IsNullOrEmpty(group.ParameterName) ? group.MenuName : group.ParameterName;
                MenuMerger.AddParameter(expressionParameters, paramName, VRCExpressionParameters.ValueType.Int, group.DefaultIndex, group.Saved, group.Synced);

                var targetMenu = FindParentMenu(group.transform, subMenuMap, expressionsMenu);
                MenuMerger.AddToggleGroupControl(targetMenu, group);
            }

            // 6. Process Radial Puppets (Float sliders)
            foreach (var puppet in radialPuppets)
            {
                if (puppet == null) continue;

                AnimationGenerator.CreateRadialPuppetClips(avatarRoot.transform, puppet, out var minClip, out var maxClip);
                ControllerGenerator.AddRadialPuppetLayer(fxController, puppet, minClip, maxClip);

                string paramName = string.IsNullOrEmpty(puppet.ParameterName) ? puppet.MenuName : puppet.ParameterName;
                MenuMerger.AddParameter(expressionParameters, paramName, VRCExpressionParameters.ValueType.Float, puppet.DefaultValue, puppet.Saved, puppet.Synced);

                var targetMenu = FindParentMenu(puppet.transform, subMenuMap, expressionsMenu);
                MenuMerger.AddRadialPuppetControl(targetMenu, puppet);
            }

            // 7. Cleanup NDMenu components from cloned object so they don't remain on final uploaded avatar
            foreach (var item in toggleItems) Object.DestroyImmediate(item);
            foreach (var item in toggleGroups) Object.DestroyImmediate(item);
            foreach (var item in radialPuppets) Object.DestroyImmediate(item);
            foreach (var item in subMenus) Object.DestroyImmediate(item);
            foreach (var item in menuRoots) Object.DestroyImmediate(item);
        }

        private static int GetHierarchyDepth(Transform t)
        {
            int depth = 0;
            while (t != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }

        private static VRCExpressionsMenu FindParentMenu(Transform transform, Dictionary<NDSubMenu, VRCExpressionsMenu> subMenuMap, VRCExpressionsMenu rootMenu)
        {
            var parent = transform.parent;
            while (parent != null)
            {
                var parentSubMenu = parent.GetComponent<NDSubMenu>();
                if (parentSubMenu != null && subMenuMap.TryGetValue(parentSubMenu, out var targetMenu))
                {
                    return targetMenu;
                }
                parent = parent.parent;
            }
            return rootMenu;
        }

        private static AnimatorController EnsureFXAnimatorController(VRCAvatarDescriptor descriptor)
        {
            for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++)
            {
                if (descriptor.baseAnimationLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    var animLayer = descriptor.baseAnimationLayers[i];
                    if (animLayer.animatorController == null || animLayer.isDefault)
                    {
                        var newController = new AnimatorController { name = "Generated_FX_Controller" };
                        animLayer.animatorController = newController;
                        animLayer.isDefault = false;
                        descriptor.baseAnimationLayers[i] = animLayer;
                        return newController;
                    }

                    // Clone existing controller to avoid modifying the original asset on disk
                    if (animLayer.animatorController is AnimatorController existingController)
                    {
                        var clonedController = Object.Instantiate(existingController);
                        animLayer.animatorController = clonedController;
                        descriptor.baseAnimationLayers[i] = animLayer;
                        return clonedController;
                    }
                }
            }

            var fallbackController = new AnimatorController { name = "Generated_FX_Controller" };
            return fallbackController;
        }

        private static void EnsureExpressionsAssets(VRCAvatarDescriptor descriptor)
        {
            if (descriptor.expressionsMenu == null)
            {
                descriptor.expressionsMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                descriptor.expressionsMenu.name = "Generated_ExpressionsMenu";
                descriptor.expressionsMenu.controls = new List<VRCExpressionsMenu.Control>();
            }
            else
            {
                descriptor.expressionsMenu = Object.Instantiate(descriptor.expressionsMenu);
            }

            if (descriptor.expressionParameters == null)
            {
                descriptor.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                descriptor.expressionParameters.name = "Generated_ExpressionParameters";
                descriptor.expressionParameters.parameters = new VRCExpressionParameters.Parameter[0];
            }
            else
            {
                descriptor.expressionParameters = Object.Instantiate(descriptor.expressionParameters);
            }
        }
    }
}
