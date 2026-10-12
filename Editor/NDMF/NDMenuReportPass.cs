using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Custom.NDMenuManager.Editor.Generators;

namespace Custom.NDMenuManager.Editor.NDMF
{
    /// <summary>
    /// Logs the assembled menu once every plugin has had its turn.
    ///
    /// NDMenuBuildPass runs in Generating, which is before Modular Avatar installs the menus that
    /// third-party prefabs contribute (Transforming) and before anything is paginated at the end of
    /// that phase. Reporting there would describe a menu nobody will ever see, so the summary is
    /// emitted in Optimizing instead: what it prints is the menu the avatar actually ships with,
    /// including whether the top level still carries entries that were meant to be inside a folder.
    ///
    /// This pass only reads, so it is safe to run last.
    /// </summary>
    public static class NDMenuReportPass
    {
        public static void Execute(BuildContext context)
        {
            var avatarRoot = context.AvatarRootObject;
            if (avatarRoot == null) return;

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return;

            // Nothing generated and nothing inherited: an empty report would just be noise.
            if (descriptor.expressionsMenu == null) return;

            UnityEngine.Debug.Log(MenuReporter.BuildReport(descriptor.expressionsMenu, descriptor.expressionParameters));

            // Problems that would otherwise only surface in game - a control whose parameter is not
            // declared, so pressing it does nothing.
            var findings = MenuValidator.BuildFindings(descriptor.expressionsMenu, descriptor.expressionParameters);
            if (!string.IsNullOrEmpty(findings))
            {
                UnityEngine.Debug.LogWarning(findings);
            }
        }
    }
}
