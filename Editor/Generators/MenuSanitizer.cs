using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Custom.NDMenuManager.Editor.Generators
{
    /// <summary>
    /// Repairs structurally invalid menu controls before the avatar is built.
    ///
    /// Unity serialisation, hand-edited assets and other plugins can all leave a control in a state
    /// that the VRChat client and common tooling (GestureManager, av3emulator) do not tolerate.
    /// VRCFury performs the same repairs in FinalizeMenuService before shipping a menu; we do them
    /// at the end of NDMenuBuildPass for the same reason.
    ///
    /// This is deliberately limited to structural repairs: it never changes what a control is
    /// supposed to do, only fills in or clears fields the SDK cannot represent.
    /// </summary>
    public static class MenuSanitizer
    {
        /// <summary>
        /// Repairs every control in the menu tree. Returns the number of repairs applied.
        /// </summary>
        public static int SanitizeAll(VRCExpressionsMenu root)
        {
            if (root == null) return 0;

            var visited = new HashSet<VRCExpressionsMenu>();
            var pending = new Queue<VRCExpressionsMenu>();
            pending.Enqueue(root);

            var repaired = 0;

            while (pending.Count > 0)
            {
                var menu = pending.Dequeue();
                if (menu == null || !visited.Add(menu)) continue;

                if (menu.controls == null)
                {
                    menu.controls = new List<VRCExpressionsMenu.Control>();
                    continue;
                }

                foreach (var control in menu.controls)
                {
                    if (control == null) continue;
                    repaired += Repair(control);

                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        if (control.subMenu != null) pending.Enqueue(control.subMenu);
                    }
                    else if (control.subMenu != null)
                    {
                        // A control that is not a sub-menu must not carry a menu reference: it would
                        // pull the whole menu asset into the build as an unused dependency and
                        // confuse viewers.
                        control.subMenu = null;
                        repaired++;
                    }
                }
            }

            return repaired;
        }

        /// <summary>Repairs one control; returns how many fields were fixed.</summary>
        private static int Repair(VRCExpressionsMenu.Control control)
        {
            var repaired = 0;

            // An out-of-range enum shows as "Button" in the editor but does nothing in game, and
            // makes av3emulator throw while it is read.
            if (!System.Enum.IsDefined(typeof(VRCExpressionsMenu.Control.ControlType), control.type))
            {
                control.type = VRCExpressionsMenu.Control.ControlType.Button;
                repaired++;
            }

            if (control.parameter == null)
            {
                control.parameter = new VRCExpressionsMenu.Control.Parameter { name = "" };
                repaired++;
            }

            if (control.subParameters == null)
            {
                control.subParameters = new VRCExpressionsMenu.Control.Parameter[0];
                repaired++;
            }

            if (control.labels == null)
            {
                control.labels = new VRCExpressionsMenu.Control.Label[0];
                repaired++;
            }

            return repaired;
        }
    }
}
