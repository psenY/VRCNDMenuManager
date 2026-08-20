using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Custom.NDMenuManager.Runtime;

namespace Custom.NDMenuManager.Editor.UI
{
    [CustomPropertyDrawer(typeof(GameObjectToggleTarget))]
    public class GameObjectToggleTargetDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 5;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.backgroundColor = new StyleColor(new Color(0.12f, 0.13f, 0.18f, 0.65f));
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;

            var objProp = property.FindPropertyRelative("targetObject");
            var activeProp = property.FindPropertyRelative("activeWhenOn");

            var objField = new PropertyField(objProp, "目标物体");
            objField.style.flexGrow = 1;
            container.Add(objField);

            var activeToggle = new PropertyField(activeProp, "开启时显示");
            activeToggle.style.marginLeft = 10;
            container.Add(activeToggle);

            return container;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            var objProp = property.FindPropertyRelative("targetObject");
            var activeProp = property.FindPropertyRelative("activeWhenOn");

            float halfWidth = (position.width - 20) * 0.65f;
            float toggleWidth = (position.width - 20) * 0.35f;

            var rObj = new Rect(position.x, position.y, halfWidth, lineHeight);
            var rActive = new Rect(position.x + halfWidth + 10, position.y, toggleWidth, lineHeight);

            EditorGUI.PropertyField(rObj, objProp, GUIContent.none);
            EditorGUI.PropertyField(rActive, activeProp, new GUIContent("开启时显示"));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomEditor(typeof(NDToggleItem))]
    public class NDToggleItemEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;

            // 1. Menu Display Card
            var displayCard = CreateSectionCard("菜单显示设置");
            displayCard.Add(new PropertyField(serializedObject.FindProperty("menuName"), "菜单显示名称"));
            displayCard.Add(new PropertyField(serializedObject.FindProperty("icon"), "菜单图标 (3D 缩略图)"));
            root.Add(displayCard);

            // 2. Parameter Settings Card
            var paramCard = CreateSectionCard("参数同步设置");
            paramCard.Add(new PropertyField(serializedObject.FindProperty("parameterName"), "参数名称 (Parameter)"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("defaultValue"), "默认开启状态"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("saved"), "跨地图保存状态 (Saved)"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("synced"), "网络同步其他玩家 (Synced)"));
            root.Add(paramCard);

            // 3. Int Sync & Protection Card
            var intCard = CreateSectionCard("互斥换装与防脱光设置");
            var useIntProp = serializedObject.FindProperty("useIntParameter");
            var intToggle = new PropertyField(useIntProp, "启用互斥换装模式 (共享 Int 参数)");
            intCard.Add(intToggle);

            var valField = new PropertyField(serializedObject.FindProperty("parameterValue"), "当前套装编号 (1, 2, 3...)");
            var allowAllOffField = new PropertyField(serializedObject.FindProperty("allowAllOff"), "允许全部脱下 (关闭则开启防脱光)");

            intCard.Add(valField);
            intCard.Add(allowAllOffField);

            void UpdateIntCardVisibility()
            {
                var isInt = useIntProp.boolValue;
                valField.style.display = isInt ? DisplayStyle.Flex : DisplayStyle.None;
                allowAllOffField.style.display = isInt ? DisplayStyle.Flex : DisplayStyle.None;
            }
            UpdateIntCardVisibility();
            intToggle.RegisterValueChangeCallback(_ => UpdateIntCardVisibility());

            root.Add(intCard);

            // 4. Targets Card
            var targetsCard = CreateSectionCard("控制目标 (物体 / 形态键 / 材质)");
            targetsCard.Add(new PropertyField(serializedObject.FindProperty("objectTargets"), "物体开关控制 (GameObjects)"));
            targetsCard.Add(new PropertyField(serializedObject.FindProperty("blendShapeTargets"), "形态键控制 (BlendShapes)"));
            targetsCard.Add(new PropertyField(serializedObject.FindProperty("materialTargets"), "材质属性控制 (Materials)"));
            root.Add(targetsCard);

            return root;
        }

        public static VisualElement CreateSectionCard(string title)
        {
            var card = new VisualElement();
            card.style.marginBottom = 10;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.backgroundColor = new StyleColor(new Color(0.16f, 0.18f, 0.25f, 0.85f));
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftColor = new StyleColor(new Color(0.25f, 0.28f, 0.4f, 0.5f));
            card.style.borderRightColor = new StyleColor(new Color(0.25f, 0.28f, 0.4f, 0.5f));
            card.style.borderTopColor = new StyleColor(new Color(0.25f, 0.28f, 0.4f, 0.5f));
            card.style.borderBottomColor = new StyleColor(new Color(0.25f, 0.28f, 0.4f, 0.5f));

            var header = new Label(title);
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new StyleColor(new Color(0.9f, 0.92f, 1f));
            header.style.marginBottom = 6;
            card.Add(header);

            return card;
        }
    }

    [CustomEditor(typeof(NDToggleGroup))]
    public class NDToggleGroupEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;

            var displayCard = NDToggleItemEditor.CreateSectionCard("互斥组显示设置");
            displayCard.Add(new PropertyField(serializedObject.FindProperty("menuName"), "互斥组显示名称"));
            displayCard.Add(new PropertyField(serializedObject.FindProperty("icon"), "组图标 (3D 缩略图)"));
            root.Add(displayCard);

            var paramCard = NDToggleItemEditor.CreateSectionCard("参数同步设置");
            paramCard.Add(new PropertyField(serializedObject.FindProperty("parameterName"), "参数名称 (Parameter)"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("defaultIndex"), "默认选项序号"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("saved"), "跨地图保存状态 (Saved)"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("synced"), "网络同步其他玩家 (Synced)"));
            root.Add(paramCard);

            var optionsCard = NDToggleItemEditor.CreateSectionCard("互斥单选列表 (Options)");
            optionsCard.Add(new PropertyField(serializedObject.FindProperty("options"), "选项列表"));
            root.Add(optionsCard);

            return root;
        }
    }

    [CustomEditor(typeof(NDRadialPuppet))]
    public class NDRadialPuppetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;

            var displayCard = NDToggleItemEditor.CreateSectionCard("菜单显示设置");
            displayCard.Add(new PropertyField(serializedObject.FindProperty("menuName"), "滑条显示名称"));
            displayCard.Add(new PropertyField(serializedObject.FindProperty("icon"), "滑条图标 (Icon)"));
            root.Add(displayCard);

            var paramCard = NDToggleItemEditor.CreateSectionCard("参数设置");
            paramCard.Add(new PropertyField(serializedObject.FindProperty("parameterName"), "参数名称 (Parameter)"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("defaultValue"), "默认数值 (0.0 ~ 1.0)"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("saved"), "跨地图保存状态 (Saved)"));
            paramCard.Add(new PropertyField(serializedObject.FindProperty("synced"), "网络同步其他玩家 (Synced)"));
            root.Add(paramCard);

            var targetsCard = NDToggleItemEditor.CreateSectionCard("连续调节目标");
            targetsCard.Add(new PropertyField(serializedObject.FindProperty("blendShapeTargets"), "形态键控制 (BlendShapes)"));
            targetsCard.Add(new PropertyField(serializedObject.FindProperty("materialTargets"), "材质属性控制 (Materials)"));
            root.Add(targetsCard);

            return root;
        }
    }

    [CustomEditor(typeof(NDSubMenu))]
    public class NDSubMenuEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;

            var displayCard = NDToggleItemEditor.CreateSectionCard("子菜单文件夹设置");
            displayCard.Add(new PropertyField(serializedObject.FindProperty("menuName"), "文件夹显示名称"));
            displayCard.Add(new PropertyField(serializedObject.FindProperty("icon"), "文件夹图标 (3D 缩略图)"));
            root.Add(displayCard);

            return root;
        }
    }
}
