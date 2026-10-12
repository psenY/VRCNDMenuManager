using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Custom.NDMenuManager.Editor.Generators
{
    /// <summary>
    /// Reports menu problems that would otherwise only show up in game.
    ///
    /// The one that costs real time is a control whose parameter is not declared in Expression
    /// Parameters: the entry looks fine in the editor, does nothing when pressed, and nothing
    /// anywhere says why. Findings are aggregated per parameter, because one undeclared parameter
    /// usually backs several entries.
    ///
    /// Read-only: this never changes the menu.
    /// </summary>
    public static class MenuValidator
    {
        private const int MaxDepth = 8;
        private const string Tag = "[NDMenuManager] ";

        /// <summary>
        /// Returns a report of menu problems, or an empty string when there are none.
        /// Sub-menu controls are skipped: their parameter field is not what drives them.
        /// </summary>
        public static string BuildFindings(VRCExpressionsMenu root,
            VRCExpressionParameters parameters, int maxFindings = 20)
        {
            if (root == null) return "";

            var findings = new List<string>();
            var missing = new Dictionary<string, List<string>>();
            var declared = DeclaredNames(parameters);
            var visited = new HashSet<VRCExpressionsMenu>();

            Walk(root, "", 0, visited, declared, missing, findings);

            foreach (var pair in missing)
            {
                var where = pair.Value;
                var sample = where.Count <= 3
                    ? string.Join(", ", where.ToArray())
                    : string.Join(", ", where.GetRange(0, 3).ToArray()) + ", ...";

                findings.Add("parameter '" + pair.Key + "' is used by " + where.Count
                    + " control(s) but is not declared in Expression Parameters, so they do nothing in game ("
                    + sample + ")");
            }

            if (findings.Count == 0) return "";

            var limit = findings.Count > maxFindings ? maxFindings : findings.Count;
            var sb = new System.Text.StringBuilder();
            sb.Append(Tag).Append(findings.Count).AppendLine(" menu finding(s):");
            for (var i = 0; i < limit; i++) sb.Append(Tag).Append("  - ").AppendLine(findings[i]);
            if (findings.Count > limit)
                sb.Append(Tag).Append("  ... ").Append(findings.Count - limit).AppendLine(" more");

            return sb.ToString().TrimEnd();
        }

        private static void Walk(VRCExpressionsMenu menu, string path, int depth,
            HashSet<VRCExpressionsMenu> visited, HashSet<string> declared,
            Dictionary<string, List<string>> missing, List<string> findings)
        {
            if (menu == null || depth > MaxDepth || !visited.Add(menu)) return;

            var here = string.IsNullOrEmpty(path) ? "(top level)" : path;

            var counts = new Dictionary<string, int>();
            if (menu.controls != null)
            {
                foreach (var control in menu.controls)
                {
                    if (control == null) continue;
                    var name = string.IsNullOrEmpty(control.name) ? "(unnamed)" : control.name;
                    counts.TryGetValue(name, out var seen);
                    counts[name] = seen + 1;
                }
            }

            foreach (var pair in counts)
            {
                if (pair.Value > 1)
                {
                    findings.Add("menu '" + here + "' has " + pair.Value + " controls named '" + pair.Key
                        + "'; players cannot tell them apart");
                }
            }

            if (menu.controls == null) return;

            foreach (var control in menu.controls)
            {
                if (control == null) continue;

                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    if (string.IsNullOrEmpty(control.name))
                        findings.Add("menu '" + here + "' has an unnamed sub-menu, which shows up as a blank entry");

                    var childPath = string.IsNullOrEmpty(control.name) ? here : here + "/" + control.name;
                    Walk(control.subMenu, childPath, depth + 1, visited, declared, missing, findings);
                    continue;
                }

                NoteReferences(control, here, declared, missing);
            }
        }

        private static void NoteReferences(VRCExpressionsMenu.Control control, string menuPath,
            HashSet<string> declared, Dictionary<string, List<string>> missing)
        {
            if (declared == null) return;

            var names = new List<string>();
            if (control.parameter != null && !string.IsNullOrEmpty(control.parameter.name))
                names.Add(control.parameter.name);

            if (control.subParameters != null)
            {
                foreach (var parameter in control.subParameters)
                {
                    if (parameter != null && !string.IsNullOrEmpty(parameter.name)) names.Add(parameter.name);
                }
            }

            foreach (var name in names)
            {
                if (declared.Contains(name)) continue;

                if (!missing.TryGetValue(name, out var where))
                {
                    where = new List<string>();
                    missing[name] = where;
                }

                var label = menuPath + "/" + (string.IsNullOrEmpty(control.name) ? "(unnamed)" : control.name);
                if (!where.Contains(label)) where.Add(label);
            }
        }

        private static HashSet<string> DeclaredNames(VRCExpressionParameters parameters)
        {
            if (parameters == null || parameters.parameters == null) return null;

            var declared = new HashSet<string>();
            foreach (var parameter in parameters.parameters)
            {
                if (parameter != null && !string.IsNullOrEmpty(parameter.name)) declared.Add(parameter.name);
            }

            return declared;
        }
    }
}
