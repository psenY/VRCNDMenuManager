using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Custom.NDMenuManager.Editor.Generators
{
    /// <summary>
    /// Decides the order of the controls inside each generated menu.
    ///
    /// Every control this plugin places is registered here with the sort key of the component that
    /// produced it, and once the pass is finished each menu is sorted by those keys. A key is
    /// (OrderIndex, scene hierarchy path):
    ///
    ///   - OrderIndex is the number the ND component carries and defaults to 0, so with nothing
    ///     filled in every control ties on the first half and the order falls back to the scene
    ///     hierarchy, which is what the plugin effectively did before.
    ///   - A lower OrderIndex moves an entry up, a higher one moves it down. A negative value lifts
    ///     an entry above the menu the avatar already had.
    ///   - Controls that were never registered (the avatar's pre-existing menu) sort as OrderIndex 0
    ///     with an empty path, and an empty path sorts first, so the existing menu stays on top and
    ///     an entry only moves above it when it is given a negative OrderIndex.
    ///
    /// Ties are broken on the original position, so equal keys never shuffle entries.
    /// </summary>
    public static class MenuOrdering
    {
        public struct Key
        {
            public int Order;
            public int[] Path;

            public Key(int order, int[] path)
            {
                Order = order;
                Path = path ?? EmptyPath;
            }
        }

        private static readonly int[] EmptyPath = new int[0];

        private static readonly Dictionary<VRCExpressionsMenu.Control, Key> Keys =
            new Dictionary<VRCExpressionsMenu.Control, Key>();

        /// <summary>Starts a fresh build: forgets the keys registered by the previous one.</summary>
        public static void Begin()
        {
            Keys.Clear();
        }

        /// <summary>Registers the sort key of a control that was just placed.</summary>
        public static void Note(VRCExpressionsMenu.Control control, Component source, int orderIndex)
        {
            if (control == null) return;
            Keys[control] = new Key(orderIndex, source != null ? PathOf(source.transform) : EmptyPath);
        }

        /// <summary>Sorts every menu in the tree by the keys registered for its controls.</summary>
        public static void Apply(VRCExpressionsMenu root)
        {
            if (root == null) return;

            var visited = new HashSet<VRCExpressionsMenu>();
            var pending = new Queue<VRCExpressionsMenu>();
            pending.Enqueue(root);

            while (pending.Count > 0)
            {
                var menu = pending.Dequeue();
                if (menu == null || !visited.Add(menu)) continue;

                SortControls(menu.controls, Keys);

                if (menu.controls == null) continue;
                foreach (var control in menu.controls)
                {
                    if (control == null || control.subMenu == null) continue;
                    if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                    pending.Enqueue(control.subMenu);
                }
            }
        }

        /// <summary>Stable sort of one menu's controls by the key each was registered with.</summary>
        public static void SortControls(List<VRCExpressionsMenu.Control> controls,
            IDictionary<VRCExpressionsMenu.Control, Key> keys)
        {
            if (controls == null || controls.Count < 2) return;

            var order = new List<int>(controls.Count);
            for (var i = 0; i < controls.Count; i++) order.Add(i);

            // List.Sort is not stable, so ties are broken on the original position explicitly.
            order.Sort((a, b) =>
            {
                var compared = Compare(KeyOf(keys, controls[a]), KeyOf(keys, controls[b]));
                return compared != 0 ? compared : a.CompareTo(b);
            });

            var sorted = new List<VRCExpressionsMenu.Control>(controls.Count);
            foreach (var index in order) sorted.Add(controls[index]);
            for (var i = 0; i < sorted.Count; i++) controls[i] = sorted[i];
        }

        /// <summary>The key registered for a control, or the neutral default.</summary>
        public static Key KeyOf(IDictionary<VRCExpressionsMenu.Control, Key> keys,
            VRCExpressionsMenu.Control control)
        {
            if (keys != null && control != null && keys.TryGetValue(control, out var key)) return key;
            return new Key(0, EmptyPath);
        }

        public static int Compare(Key a, Key b)
        {
            if (a.Order != b.Order) return a.Order < b.Order ? -1 : 1;
            return ComparePaths(a.Path, b.Path);
        }

        /// <summary>Lexicographic comparison; an empty path counts as coming first.</summary>
        public static int ComparePaths(int[] a, int[] b)
        {
            var aEmpty = a == null || a.Length == 0;
            var bEmpty = b == null || b.Length == 0;
            if (aEmpty) return bEmpty ? 0 : -1;
            if (bEmpty) return 1;

            var shared = a.Length < b.Length ? a.Length : b.Length;
            for (var i = 0; i < shared; i++)
            {
                if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
            }

            if (a.Length == b.Length) return 0;
            return a.Length < b.Length ? -1 : 1;
        }

        /// <summary>Sibling indices from the top of the hierarchy down to this transform.</summary>
        public static int[] PathOf(Transform transform)
        {
            if (transform == null) return EmptyPath;

            var indices = new List<int>();
            var cursor = transform;
            while (cursor != null)
            {
                indices.Add(cursor.GetSiblingIndex());
                cursor = cursor.parent;
            }

            indices.Reverse();
            return indices.ToArray();
        }
    }
}
