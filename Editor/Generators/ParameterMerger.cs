using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Custom.NDMenuManager.Editor.Generators
{
    /// <summary>
    /// Adds generated expression parameters without silently losing information.
    ///
    /// Re-adding a parameter that already exists is normal: several ND components legitimately share
    /// one parameter, and the avatar author or another plugin may have declared it first. What must
    /// not happen silently is a *type* collision — an existing Bool next to a requested Int leaves
    /// the avatar with a parameter its menu cannot drive, and the only symptom is a toggle that does
    /// nothing in game.
    ///
    /// Modular Avatar reports a type conflict as a build error and keeps the existing parameter;
    /// VRCFury throws. We take the Modular Avatar route: report and continue, so a single build
    /// lists every conflict instead of stopping at the first.
    /// </summary>
    public static class ParameterMerger
    {
        public enum MergeResult
        {
            /// <summary>The parameter was absent and has been added.</summary>
            Added,

            /// <summary>An identical parameter was already present.</summary>
            AlreadyPresent,

            /// <summary>Same type, different saved/synced/default; the existing one was kept.</summary>
            SettingMismatch,

            /// <summary>Same name with a different type; the existing one was kept.</summary>
            TypeConflict
        }

        public static MergeResult Add(VRCExpressionParameters expParams, string name,
            VRCExpressionParameters.ValueType type, float defaultValue, bool saved, bool synced,
            UnityEngine.Object context = null)
        {
            if (expParams == null || string.IsNullOrEmpty(name)) return MergeResult.AlreadyPresent;

            var existing = expParams.FindParameter(name);
            if (existing == null)
            {
                var parameters = new List<VRCExpressionParameters.Parameter>(
                    expParams.parameters ?? new VRCExpressionParameters.Parameter[0]);

                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = name,
                    valueType = type,
                    defaultValue = defaultValue,
                    saved = saved,
                    networkSynced = synced
                });

                expParams.parameters = parameters.ToArray();
                return MergeResult.Added;
            }

            if (existing.valueType != type)
            {
                Debug.LogError(
                    $"[NDMenuManager] Expression parameter '{name}' already exists as {existing.valueType}, " +
                    $"but {type} was requested. The existing parameter is kept, so the control asking for " +
                    $"{type} will do nothing in game - rename one of them.",
                    context);
                return MergeResult.TypeConflict;
            }

            if (existing.saved != saved || existing.networkSynced != synced ||
                !Mathf.Approximately(existing.defaultValue, defaultValue))
            {
                Debug.LogWarning(
                    $"[NDMenuManager] Expression parameter '{name}' already exists with saved={existing.saved}, " +
                    $"synced={existing.networkSynced}, default={existing.defaultValue}; keeping it instead of " +
                    $"saved={saved}, synced={synced}, default={defaultValue}.",
                    context);
                return MergeResult.SettingMismatch;
            }

            return MergeResult.AlreadyPresent;
        }
    }
}
