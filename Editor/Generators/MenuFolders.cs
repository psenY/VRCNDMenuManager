using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Custom.NDMenuManager.Editor.Generators
{
    /// <summary>
    /// Creates — or reuses — the sub-menu that an NDSubMenu component turns into.
    ///
    /// Several NDSubMenu components sharing a name under the same parent are meant to be one folder:
    /// that is how a wardrobe assembled from a dozen prefabs ends up with a single "Wardrobe" entry
    /// instead of one entry per prefab. Reuse is decided on a normalised name, because menu names in
    /// the wild carry Unity rich-text tags and stray whitespace (VRCFury matches names the same way).
    ///
    /// Reuse is always scoped to the parent, so two different folders may both be called "Suit" as
    /// long as they live under different parents.
    /// </summary>
    public static class MenuFolders
    {
        private static readonly Regex RichText = new Regex("<.*?>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Lower-cases a menu name, drops Unity rich-text tags and collapses whitespace, so
        /// "&lt;size=20&gt;Wardrobe" and "wardrobe" are recognised as the same folder.
        /// </summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var text = RichText.Replace(name, " ");
            text = text.Replace("\\n", " ").Replace("\n", " ").Replace("\r", " ");
            text = text.ToLowerInvariant();
            return Spaces.Replace(text, " ").Trim();
        }

        /// <summary>
        /// Returns the existing sub-menu with this name under this parent, or null. Unnamed folders
        /// never match: several of them are almost always meant to stay separate.
        /// </summary>
        public static VRCExpressionsMenu Find(VRCExpressionsMenu parentMenu, string name)
        {
            if (parentMenu == null || parentMenu.controls == null) return null;

            var wanted = Normalize(name);
            if (wanted.Length == 0) return null;

            foreach (var control in parentMenu.controls)
            {
                if (control == null || control.subMenu == null) continue;
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                if (Normalize(control.name) == wanted) return control.subMenu;
            }

            return null;
        }

        /// <summary>
        /// Gets the sub-menu for this name under this parent, creating and attaching it the first
        /// time. <paramref name="reused"/> reports which of the two happened.
        /// </summary>
        public static VRCExpressionsMenu GetOrCreate(VRCExpressionsMenu parentMenu, string name,
            Texture2D icon, out bool reused)
        {
            reused = false;
            if (parentMenu == null) return null;

            var existing = Find(parentMenu, name);
            if (existing != null)
            {
                reused = true;
                return existing;
            }

            if (parentMenu.controls == null)
            {
                parentMenu.controls = new List<VRCExpressionsMenu.Control>();
            }

            var subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            subMenu.name = "SubMenu_" + name;
            subMenu.controls = new List<VRCExpressionsMenu.Control>();

            parentMenu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = name,
                icon = icon,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = subMenu,

                // Keep every array non-null: a null subParameters makes some viewers throw.
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                labels = new VRCExpressionsMenu.Control.Label[0]
            });

            return subMenu;
        }
    }
}
