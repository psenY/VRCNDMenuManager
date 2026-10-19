using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Custom.NDMenuManager.Editor.Generators
{
    /// <summary>
    /// Final-pass menu pagination.
    ///
    /// VRChat renders at most VRCExpressionsMenu.MAX_CONTROLS controls on one menu page. If a menu
    /// holds more than that, the client drops or reshuffles the overflow, so we split it ourselves
    /// before the avatar is built and link the pages with a "next page" sub-menu control.
    ///
    /// The algorithm mirrors VRCFury's MenuSplitter.SplitMenus:
    ///   - a split page keeps MAX_CONTROLS - 1 controls, the last slot is the next-page link;
    ///   - the remainder moves into a new page, which is split again if it is still too big;
    ///   - the loop stops at MAX_CONTROLS - 1, so a trailing chunk of exactly MAX_CONTROLS stays
    ///     whole on its page instead of becoming "7 items plus a page holding one item" (the same
    ///     outcome KaoruBoy's VRC-Expression-Menu-As-Code reaches with an explicit special case).
    ///
    /// VRCFury runs its splitter at the very end of its own pipeline; this must likewise stay at
    /// the end of NDMenuBuildPass, after every MenuMerger call, or freshly added controls can
    /// overflow a page again with nothing left to fix it.
    /// </summary>
    public static class MenuPager
    {
        public const string DefaultNextPageText = "Next Page";

        // Guard against SDK releases that report an unreasonable MAX_CONTROLS.
        private const int MaxReasonableControls = 1000;
        private const int FallbackControlsPerPage = 8;
        private const int MinControlsPerPage = 2;

        /// <summary>Controls VRChat can show on one page, with a sanity fallback.</summary>
        public static int ControlsPerPage
        {
            get
            {
                var n = VRCExpressionsMenu.MAX_CONTROLS;
                if (n > MaxReasonableControls || n < MinControlsPerPage) n = FallbackControlsPerPage;
                return n;
            }
        }

        /// <summary>
        /// Splits every page of the menu tree that exceeds the per-page control limit.
        /// Returns the number of next-page menus created.
        /// </summary>
        public static int SplitAll(VRCExpressionsMenu root,
            string nextPageText = DefaultNextPageText, Texture2D nextPageIcon = null)
        {
            if (root == null) return 0;

            var perPage = ControlsPerPage;
            var visited = new HashSet<VRCExpressionsMenu>();
            var pending = new Queue<VRCExpressionsMenu>();
            pending.Enqueue(root);

            var created = 0;

            while (pending.Count > 0)
            {
                var menu = pending.Dequeue();
                // Shared sub-menus and reference cycles are split at most once.
                if (menu == null || !visited.Add(menu)) continue;

                // Split this page and every page that follows it, naming them Page 2, 3, ...
                var chain = new List<VRCExpressionsMenu> { menu };
                var page = menu;
                var pageNum = 2;

                while (page.controls != null && page.controls.Count > perPage)
                {
                    var nextPage = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    nextPage.name = menu.name + " (Page " + pageNum++ + ")";
                    nextPage.controls = new List<VRCExpressionsMenu.Control>();

                    // Free the last slot for the next-page link and move the tail to the new page.
                    while (page.controls.Count > perPage - 1)
                    {
                        var last = page.controls.Count - 1;
                        nextPage.controls.Insert(0, page.controls[last]);
                        page.controls.RemoveAt(last);
                    }

                    page.controls.Add(MakeNextPageControl(nextPageText, nextPageIcon, nextPage));
                    created++;

                    chain.Add(nextPage);
                    page = nextPage;
                }

                // The pages we just built are part of this traversal already.
                foreach (var built in chain) visited.Add(built);

                // Queue every real sub-menu for its own pass; next-page links are already visited.
                foreach (var built in chain)
                {
                    if (built.controls == null) continue;
                    foreach (var control in built.controls)
                    {
                        if (control == null || control.subMenu == null) continue;
                        if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                        pending.Enqueue(control.subMenu);
                    }
                }
            }

            return created;
        }

        private static VRCExpressionsMenu.Control MakeNextPageControl(string text, Texture2D icon,
            VRCExpressionsMenu nextPage)
        {
            return new VRCExpressionsMenu.Control
            {
                name = string.IsNullOrEmpty(text) ? DefaultNextPageText : text,
                icon = icon,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = nextPage,

                // Keep every array non-null: a null subParameters makes some viewers throw.
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                labels = new VRCExpressionsMenu.Control.Label[0]
            };
        }
    }
}
