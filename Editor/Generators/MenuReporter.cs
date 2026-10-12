using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Custom.NDMenuManager.Editor.Generators
{
    /// <summary>
    /// Renders a short summary of the menu that is about to be built.
    ///
    /// Everything this plugin generates happens inside a build pass, so the only way to see the
    /// result used to be running ManualProcessAvatar by hand. Logging the finished tree means the
    /// answer ("what does my top level actually contain, and how did the pages end up") is one
    /// Editor.log read away, and it is the same summary every time.
    ///
    /// The walk is cycle-safe and only reports each menu once.
    /// </summary>
    public static class MenuReporter
    {
        private const int MaxDepth = 8;

        public static string BuildReport(VRCExpressionsMenu root,
            VRCExpressionParameters parameters = null, int maxLines = 60)
        {
            if (root == null) return "[NDMenuManager] Built menu: (none)";

            var visited = new HashSet<VRCExpressionsMenu>();
            var lines = new List<string>();
            var omitted = 0;
            var menus = 0;
            var controls = 0;

            Walk(root, 0, visited, lines, maxLines, ref omitted, ref menus, ref controls);

            var topLevel = new List<string>();
            if (root.controls != null)
            {
                foreach (var control in root.controls)
                {
                    if (control != null) topLevel.Add(control.name);
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("[NDMenuManager] Built menu: ")
                .Append(topLevel.Count).Append(" top-level entries, ")
                .Append(menus).Append(" menus, ")
                .Append(controls).Append(" controls")
                .AppendLine();
            sb.Append("[NDMenuManager]   top-level: ").Append(string.Join(", ", topLevel)).AppendLine();

            foreach (var line in lines) sb.AppendLine(line);
            if (omitted > 0) sb.Append("[NDMenuManager]   ... ").Append(omitted).AppendLine(" more menus omitted");

            if (parameters != null)
            {
                var count = parameters.parameters == null ? 0 : parameters.parameters.Length;
                sb.Append("[NDMenuManager] Expression parameters: ").Append(count).AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static void Walk(VRCExpressionsMenu menu, int depth, HashSet<VRCExpressionsMenu> visited,
            List<string> lines, int maxLines, ref int omitted, ref int menus, ref int controls)
        {
            if (menu == null || depth > MaxDepth || !visited.Add(menu)) return;

            var count = menu.controls == null ? 0 : menu.controls.Count;
            menus++;
            controls += count;

            if (lines.Count < maxLines)
            {
                lines.Add("[NDMenuManager]   " + new string(' ', depth * 2) + "- " + menu.name + " (" + count + " controls)");
            }
            else
            {
                omitted++;
            }

            if (menu.controls == null) return;

            foreach (var control in menu.controls)
            {
                if (control == null) continue;
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                Walk(control.subMenu, depth + 1, visited, lines, maxLines, ref omitted, ref menus, ref controls);
            }
        }
    }
}
