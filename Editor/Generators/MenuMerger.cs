using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Custom.NDMenuManager.Runtime;

namespace Custom.NDMenuManager.Editor.Generators
{
    public static class MenuMerger
    {
        public static void AddParameter(VRCExpressionParameters expParams, string name, VRCExpressionParameters.ValueType type, float defaultValue, bool saved, bool synced)
        {
            if (expParams == null || string.IsNullOrEmpty(name)) return;

            // Check if already exists
            if (expParams.FindParameter(name) != null) return;

            var paramList = new List<VRCExpressionParameters.Parameter>(expParams.parameters ?? new VRCExpressionParameters.Parameter[0]);

            var newParam = new VRCExpressionParameters.Parameter
            {
                name = name,
                valueType = type,
                defaultValue = defaultValue,
                saved = saved,
                networkSynced = synced
            };

            paramList.Add(newParam);
            expParams.parameters = paramList.ToArray();
        }

        public static void AddToggleControl(VRCExpressionsMenu menu, NDToggleItem toggle, bool insertAtTop = false)
        {
            if (menu == null || toggle == null) return;
            string paramName = string.IsNullOrEmpty(toggle.ParameterName) ? toggle.MenuName : toggle.ParameterName;

            var control = new VRCExpressionsMenu.Control
            {
                name = toggle.MenuName,
                icon = toggle.Icon,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName },
                value = toggle.UseIntParameter ? toggle.ParameterValue : 1f
            };

            if (insertAtTop || toggle.UseIntParameter)
            {
                InsertControlAtTopOfMenu(menu, control);
            }
            else
            {
                AddControlToMenu(menu, control);
            }
        }

        private static void InsertControlAtTopOfMenu(VRCExpressionsMenu menu, VRCExpressionsMenu.Control control)
        {
            if (menu.controls == null)
            {
                menu.controls = new List<VRCExpressionsMenu.Control>();
            }

            if (menu.controls.Count >= 8)
            {
                Debug.LogWarning($"[NDMenuManager] Menu '{menu.name}' has reached the 8-control limit! Control '{control.name}' might not be displayed properly unless split.");
            }

            menu.controls.Insert(0, control);
        }

        public static void AddRadialPuppetControl(VRCExpressionsMenu menu, NDRadialPuppet puppet)
        {
            if (menu == null || puppet == null) return;
            string paramName = string.IsNullOrEmpty(puppet.ParameterName) ? puppet.MenuName : puppet.ParameterName;

            var control = new VRCExpressionsMenu.Control
            {
                name = puppet.MenuName,
                icon = puppet.Icon,
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                subParameters = new VRCExpressionsMenu.Control.Parameter[]
                {
                    new VRCExpressionsMenu.Control.Parameter { name = paramName }
                }
            };

            AddControlToMenu(menu, control);
        }

        public static void AddToggleGroupControl(VRCExpressionsMenu parentMenu, NDToggleGroup group)
        {
            if (parentMenu == null || group == null) return;
            string paramName = string.IsNullOrEmpty(group.ParameterName) ? group.MenuName : group.ParameterName;

            // Create a sub-menu for the outfit options
            var groupSubMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            groupSubMenu.name = $"SubMenu_{group.MenuName}";
            groupSubMenu.controls = new List<VRCExpressionsMenu.Control>();

            for (int i = 0; i < group.options.Count; i++)
            {
                var opt = group.options[i];
                var optControl = new VRCExpressionsMenu.Control
                {
                    name = opt.optionName,
                    icon = opt.icon,
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName },
                    value = i
                };
                AddControlToMenu(groupSubMenu, optControl);
            }

            var subMenuControl = new VRCExpressionsMenu.Control
            {
                name = group.MenuName,
                icon = group.Icon,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = groupSubMenu
            };

            AddControlToMenu(parentMenu, subMenuControl);
        }

        public static VRCExpressionsMenu CreateSubMenuControl(VRCExpressionsMenu parentMenu, string name, Texture2D icon)
        {
            var subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            subMenu.name = $"SubMenu_{name}";
            subMenu.controls = new List<VRCExpressionsMenu.Control>();

            var subMenuControl = new VRCExpressionsMenu.Control
            {
                name = name,
                icon = icon,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = subMenu
            };

            AddControlToMenu(parentMenu, subMenuControl);
            return subMenu;
        }

        private static void AddControlToMenu(VRCExpressionsMenu menu, VRCExpressionsMenu.Control control)
        {
            if (menu.controls == null)
            {
                menu.controls = new List<VRCExpressionsMenu.Control>();
            }

            if (menu.controls.Count >= 8)
            {
                Debug.LogWarning($"[NDMenuManager] Menu '{menu.name}' has reached the 8-control limit! Control '{control.name}' might not be displayed properly unless split.");
            }

            menu.controls.Add(control);
        }
    }
}
