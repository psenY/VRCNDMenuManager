using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Custom.NDMenuManager.Runtime;

namespace Custom.NDMenuManager.Editor.UI
{
    [CustomPropertyDrawer(typeof(BlendShapeToggleTarget))]
    public class BlendShapeToggleTargetDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.marginBottom = 6;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.paddingTop = 6;
            container.style.paddingBottom = 6;
            container.style.backgroundColor = new StyleColor(new Color(0.12f, 0.13f, 0.18f, 0.6f));
            container.style.borderTopLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderBottomRightRadius = 6;

            var smrProp = property.FindPropertyRelative("skinnedMeshRenderer");
            var nameProp = property.FindPropertyRelative("blendShapeName");
            var offProp = property.FindPropertyRelative("valueWhenOff");
            var onProp = property.FindPropertyRelative("valueWhenOn");

            var smrField = new PropertyField(smrProp, "渲染器 (Mesh)");
            container.Add(smrField);

            var dropdownContainer = new VisualElement();
            dropdownContainer.style.marginTop = 3;
            dropdownContainer.style.marginBottom = 3;
            container.Add(dropdownContainer);

            void UpdateDropdown()
            {
                dropdownContainer.Clear();
                var smr = smrProp.objectReferenceValue as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                {
                    var mesh = smr.sharedMesh;
                    var shapeNames = new List<string>();
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        shapeNames.Add(mesh.GetBlendShapeName(i));
                    }

                    string currentName = nameProp.stringValue;
                    int initialIndex = shapeNames.IndexOf(currentName);
                    if (initialIndex < 0)
                    {
                        if (string.IsNullOrEmpty(currentName) && shapeNames.Count > 0)
                        {
                            currentName = shapeNames[0];
                            nameProp.stringValue = currentName;
                            nameProp.serializedObject.ApplyModifiedProperties();
                            initialIndex = 0;
                        }
                        else
                        {
                            initialIndex = 0;
                        }
                    }

                    var dropdown = new PopupField<string>("选择形态键 (BlendShape)", shapeNames, initialIndex >= 0 ? initialIndex : 0);
                    dropdown.RegisterValueChangedCallback(evt =>
                    {
                        nameProp.stringValue = evt.newValue;
                        nameProp.serializedObject.ApplyModifiedProperties();
                    });
                    dropdownContainer.Add(dropdown);
                }
                else
                {
                    var fallbackField = new PropertyField(nameProp, "形态键名称 (请先指定Mesh)");
                    dropdownContainer.Add(fallbackField);
                }
            }

            UpdateDropdown();

            smrField.RegisterValueChangeCallback(_ =>
            {
                property.serializedObject.ApplyModifiedProperties();
                UpdateDropdown();
            });

            var offSlider = new Slider("关闭时数值 (0~100)", 0f, 100f);
            offSlider.BindProperty(offProp);
            offSlider.style.marginTop = 2;
            container.Add(offSlider);

            var onSlider = new Slider("开启时数值 (0~100)", 0f, 100f);
            onSlider.BindProperty(onProp);
            onSlider.style.marginTop = 2;
            container.Add(onSlider);

            return container;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var smrProp = property.FindPropertyRelative("skinnedMeshRenderer");
            var nameProp = property.FindPropertyRelative("blendShapeName");
            var offProp = property.FindPropertyRelative("valueWhenOff");
            var onProp = property.FindPropertyRelative("valueWhenOn");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            var r = new Rect(position.x, position.y, position.width, lineHeight);

            EditorGUI.PropertyField(r, smrProp, new GUIContent("渲染器 (Mesh)"));
            r.y += lineHeight + spacing;

            var smr = smrProp.objectReferenceValue as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
            {
                var mesh = smr.sharedMesh;
                var names = new string[mesh.blendShapeCount];
                for (int i = 0; i < mesh.blendShapeCount; i++) names[i] = mesh.GetBlendShapeName(i);

                int idx = System.Array.IndexOf(names, nameProp.stringValue);
                if (idx < 0) idx = 0;

                int newIdx = EditorGUI.Popup(r, "选择形态键", idx, names);
                if (newIdx >= 0 && newIdx < names.Length)
                {
                    nameProp.stringValue = names[newIdx];
                }
            }
            else
            {
                EditorGUI.PropertyField(r, nameProp, new GUIContent("形态键名称"));
            }

            r.y += lineHeight + spacing;
            EditorGUI.Slider(r, offProp, 0f, 100f, new GUIContent("关闭时数值"));
            r.y += lineHeight + spacing;
            EditorGUI.Slider(r, onProp, 0f, 100f, new GUIContent("开启时数值"));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 4 + 4f;
        }
    }

    [CustomPropertyDrawer(typeof(BlendShapePuppetTarget))]
    public class BlendShapePuppetTargetDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.marginBottom = 6;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.paddingTop = 6;
            container.style.paddingBottom = 6;
            container.style.backgroundColor = new StyleColor(new Color(0.12f, 0.13f, 0.18f, 0.6f));
            container.style.borderTopLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderBottomRightRadius = 6;

            var smrProp = property.FindPropertyRelative("skinnedMeshRenderer");
            var nameProp = property.FindPropertyRelative("blendShapeName");
            var minProp = property.FindPropertyRelative("minValue");
            var maxProp = property.FindPropertyRelative("maxValue");

            var smrField = new PropertyField(smrProp, "渲染器 (Mesh)");
            container.Add(smrField);

            var dropdownContainer = new VisualElement();
            dropdownContainer.style.marginTop = 3;
            dropdownContainer.style.marginBottom = 3;
            container.Add(dropdownContainer);

            void UpdateDropdown()
            {
                dropdownContainer.Clear();
                var smr = smrProp.objectReferenceValue as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                {
                    var mesh = smr.sharedMesh;
                    var shapeNames = new List<string>();
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        shapeNames.Add(mesh.GetBlendShapeName(i));
                    }

                    string currentName = nameProp.stringValue;
                    int initialIndex = shapeNames.IndexOf(currentName);
                    if (initialIndex < 0)
                    {
                        if (string.IsNullOrEmpty(currentName) && shapeNames.Count > 0)
                        {
                            currentName = shapeNames[0];
                            nameProp.stringValue = currentName;
                            nameProp.serializedObject.ApplyModifiedProperties();
                            initialIndex = 0;
                        }
                        else
                        {
                            initialIndex = 0;
                        }
                    }

                    var dropdown = new PopupField<string>("选择形态键 (BlendShape)", shapeNames, initialIndex >= 0 ? initialIndex : 0);
                    dropdown.RegisterValueChangedCallback(evt =>
                    {
                        nameProp.stringValue = evt.newValue;
                        nameProp.serializedObject.ApplyModifiedProperties();
                    });
                    dropdownContainer.Add(dropdown);
                }
                else
                {
                    var fallbackField = new PropertyField(nameProp, "形态键名称 (请先指定Mesh)");
                    dropdownContainer.Add(fallbackField);
                }
            }

            UpdateDropdown();

            smrField.RegisterValueChangeCallback(_ =>
            {
                property.serializedObject.ApplyModifiedProperties();
                UpdateDropdown();
            });

            var minSlider = new Slider("0% 滑条对应数值", 0f, 100f);
            minSlider.BindProperty(minProp);
            minSlider.style.marginTop = 2;
            container.Add(minSlider);

            var maxSlider = new Slider("100% 滑条对应数值", 0f, 100f);
            maxSlider.BindProperty(maxProp);
            maxSlider.style.marginTop = 2;
            container.Add(maxSlider);

            return container;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var smrProp = property.FindPropertyRelative("skinnedMeshRenderer");
            var nameProp = property.FindPropertyRelative("blendShapeName");
            var minProp = property.FindPropertyRelative("minValue");
            var maxProp = property.FindPropertyRelative("maxValue");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            var r = new Rect(position.x, position.y, position.width, lineHeight);

            EditorGUI.PropertyField(r, smrProp, new GUIContent("渲染器 (Mesh)"));
            r.y += lineHeight + spacing;

            var smr = smrProp.objectReferenceValue as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
            {
                var mesh = smr.sharedMesh;
                var names = new string[mesh.blendShapeCount];
                for (int i = 0; i < mesh.blendShapeCount; i++) names[i] = mesh.GetBlendShapeName(i);

                int idx = System.Array.IndexOf(names, nameProp.stringValue);
                if (idx < 0) idx = 0;

                int newIdx = EditorGUI.Popup(r, "选择形态键", idx, names);
                if (newIdx >= 0 && newIdx < names.Length)
                {
                    nameProp.stringValue = names[newIdx];
                }
            }
            else
            {
                EditorGUI.PropertyField(r, nameProp, new GUIContent("形态键名称"));
            }

            r.y += lineHeight + spacing;
            EditorGUI.Slider(r, minProp, 0f, 100f, new GUIContent("最小值"));
            r.y += lineHeight + spacing;
            EditorGUI.Slider(r, maxProp, 0f, 100f, new GUIContent("最大值"));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 4 + 4f;
        }
    }
}
