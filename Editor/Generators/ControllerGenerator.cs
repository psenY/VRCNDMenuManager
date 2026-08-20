using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Custom.NDMenuManager.Runtime;

namespace Custom.NDMenuManager.Editor.Generators
{
    public static class ControllerGenerator
    {
        public static void AddParameterIfMissing(AnimatorController controller, string paramName, AnimatorControllerParameterType type, object defaultValue = null)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == paramName) return;
            }

            var param = new AnimatorControllerParameter
            {
                name = paramName,
                type = type
            };

            if (defaultValue != null)
            {
                if (defaultValue is bool b) param.defaultBool = b;
                else if (defaultValue is int i) param.defaultInt = i;
                else if (defaultValue is float f) param.defaultFloat = f;
            }

            controller.AddParameter(param);
        }

        public static void AddToggleLayer(AnimatorController controller, NDToggleItem toggle, AnimationClip offClip, AnimationClip onClip, bool writeDefaults = false)
        {
            string paramName = string.IsNullOrEmpty(toggle.ParameterName) ? toggle.MenuName : toggle.ParameterName;
            AddParameterIfMissing(controller, paramName, AnimatorControllerParameterType.Bool, toggle.DefaultValue);

            var layerName = $"ND_{toggle.MenuName}";
            var stateMachine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

            var offState = stateMachine.AddState("Off");
            offState.motion = offClip;
            offState.writeDefaultValues = writeDefaults;

            var onState = stateMachine.AddState("On");
            onState.motion = onClip;
            onState.writeDefaultValues = writeDefaults;

            stateMachine.defaultState = toggle.DefaultValue ? onState : offState;

            var transToOn = stateMachine.AddAnyStateTransition(onState);
            transToOn.hasExitTime = false;
            transToOn.duration = 0f;
            transToOn.canTransitionToSelf = false;
            transToOn.AddCondition(AnimatorConditionMode.If, 0, paramName);

            var transToOff = stateMachine.AddAnyStateTransition(offState);
            transToOff.hasExitTime = false;
            transToOff.duration = 0f;
            transToOff.canTransitionToSelf = false;
            transToOff.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);

            controller.AddLayer(layer);
        }

        public static void AddToggleGroupLayer(AnimatorController controller, NDToggleGroup group, List<AnimationClip> optionClips, bool writeDefaults = false)
        {
            if (group.options.Count == 0 || optionClips.Count == 0) return;

            string paramName = string.IsNullOrEmpty(group.ParameterName) ? group.MenuName : group.ParameterName;
            AddParameterIfMissing(controller, paramName, AnimatorControllerParameterType.Int, group.DefaultIndex);

            var layerName = $"ND_{group.MenuName}";
            var stateMachine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

            AnimatorState defaultState = null;

            for (int i = 0; i < group.options.Count; i++)
            {
                var option = group.options[i];
                var state = stateMachine.AddState(option.optionName);
                state.motion = optionClips[i];
                state.writeDefaultValues = writeDefaults;

                if (i == group.DefaultIndex)
                {
                    defaultState = state;
                }

                var trans = stateMachine.AddAnyStateTransition(state);
                trans.hasExitTime = false;
                trans.duration = 0f;
                trans.canTransitionToSelf = false;
                trans.AddCondition(AnimatorConditionMode.Equals, i, paramName);
            }

            if (defaultState != null)
            {
                stateMachine.defaultState = defaultState;
            }

            controller.AddLayer(layer);
        }

        public static void AddSharedIntToggleLayer(
            AnimatorController controller,
            string paramName,
            int defaultVal,
            AnimationClip allOffClip,
            List<(int val, string name, AnimationClip clip)> states,
            bool allowAllOff = false,
            bool writeDefaults = false)
        {
            if (states == null || states.Count == 0) return;

            AddParameterIfMissing(controller, paramName, AnimatorControllerParameterType.Int, defaultVal);

            var layerName = $"ND_{paramName}";
            var stateMachine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

            AnimatorState defaultState = null;

            if (allowAllOff)
            {
                // 1. Allow All Off Mode: State 0 turns off all clothes
                var offState = stateMachine.AddState("All_Off");
                offState.motion = allOffClip;
                offState.writeDefaultValues = writeDefaults;

                var transToOff = stateMachine.AddAnyStateTransition(offState);
                transToOff.hasExitTime = false;
                transToOff.duration = 0f;
                transToOff.canTransitionToSelf = false;
                transToOff.AddCondition(AnimatorConditionMode.Equals, 0, paramName);

                foreach (var (val, name, clip) in states)
                {
                    var state = stateMachine.AddState(name);
                    state.motion = clip;
                    state.writeDefaultValues = writeDefaults;

                    if (val == defaultVal) defaultState = state;

                    var trans = stateMachine.AddAnyStateTransition(state);
                    trans.hasExitTime = false;
                    trans.duration = 0f;
                    trans.canTransitionToSelf = false;
                    trans.AddCondition(AnimatorConditionMode.Equals, val, paramName);
                }

                stateMachine.defaultState = defaultState != null ? defaultState : offState;
            }
            else
            {
                // 2. Per-Outfit Contextual Protection Mode:
                // Clicking the active toggle stays on the CURRENT outfit (never reverts to another outfit or off)
                foreach (var (val, name, clip) in states)
                {
                    var state = stateMachine.AddState(name);
                    state.motion = clip;
                    state.writeDefaultValues = writeDefaults;

                    if (val == defaultVal) defaultState = state;

                    var trans = stateMachine.AddAnyStateTransition(state);
                    trans.hasExitTime = false;
                    trans.duration = 0f;
                    trans.canTransitionToSelf = false;
                    trans.AddCondition(AnimatorConditionMode.Equals, val, paramName);

                    // Recovery State for this specific outfit
                    var recoveryState = stateMachine.AddState($"{name}_KeepActive");
                    recoveryState.motion = clip;
                    recoveryState.writeDefaultValues = writeDefaults;

                    var driver = recoveryState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    if (driver != null)
                    {
                        if (driver.parameters == null)
                        {
                            driver.parameters = new List<VRCAvatarParameterDriver.Parameter>();
                        }
                        driver.parameters.Add(new VRCAvatarParameterDriver.Parameter
                        {
                            name = paramName,
                            value = val,
                            type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set
                        });
                    }

                    var transToRecovery = state.AddTransition(recoveryState);
                    transToRecovery.hasExitTime = false;
                    transToRecovery.duration = 0f;
                    transToRecovery.AddCondition(AnimatorConditionMode.Equals, 0, paramName);

                    var transBack = recoveryState.AddTransition(state);
                    transBack.hasExitTime = false;
                    transBack.duration = 0f;
                    transBack.AddCondition(AnimatorConditionMode.Equals, val, paramName);
                }

                if (defaultState == null && states.Count > 0)
                {
                    // Fallback to first state if no default match
                    defaultState = stateMachine.states.Length > 0 ? stateMachine.states[0].state : null;
                }

                stateMachine.defaultState = defaultState;
            }
            controller.AddLayer(layer);
        }

        public static void AddRadialPuppetLayer(AnimatorController controller, NDRadialPuppet puppet, AnimationClip minClip, AnimationClip maxClip, bool writeDefaults = false)
        {
            string paramName = string.IsNullOrEmpty(puppet.ParameterName) ? puppet.MenuName : puppet.ParameterName;
            AddParameterIfMissing(controller, paramName, AnimatorControllerParameterType.Float, puppet.DefaultValue);

            var layerName = $"ND_{puppet.MenuName}";
            var stateMachine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

            var blendTree = new BlendTree
            {
                name = $"{puppet.MenuName}_BlendTree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = paramName,
                useAutomaticThresholds = false
            };

            blendTree.AddChild(minClip, 0f);
            blendTree.AddChild(maxClip, 1f);

            var state = stateMachine.AddState("Blend");
            state.motion = blendTree;
            state.writeDefaultValues = writeDefaults;
            stateMachine.defaultState = state;

            controller.AddLayer(layer);
        }
    }
}
