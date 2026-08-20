using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Custom.NDMenuManager.Runtime;
using Custom.NDMenuManager.Editor.Generators;

namespace Custom.NDMenuManager.Editor.UI
{
    public class MenuItemNode
    {
        public int id;
        public MonoBehaviour component;
        public string name;
        public string paramName;
        public MenuItemType itemType;
        public int bitCost;
        public Texture2D icon;
    }

    public class MenuManagerWindow : EditorWindow
    {
        public enum WizardMode
        {
            Exclusive, // Multi-branch switcher (Outfits, Hairstyles, Body types)
            Props      // Batch independent props/toggles
        }

        public class WizardItem
        {
            public GameObject gameObject;
            public string displayName;
        }

        [MenuItem("Tools/psenY7 ND Menu Manager", priority = 100)]
        public static void OpenWindow()
        {
            var window = GetWindow<MenuManagerWindow>();
            window.titleContent = new GUIContent("psenY7 ND Menu Manager", EditorGUIUtility.IconContent("d_CustomTool").image);
            window.minSize = new Vector2(1050, 680);

            if (window.position.width < 1200 || window.position.height < 750)
            {
                var pos = window.position;
                pos.width = 1320;
                pos.height = 840;
                window.position = pos;
            }

            window.Show();
        }

        public static void OpenWizardView()
        {
            var window = GetWindow<MenuManagerWindow>();
            OpenWindow();
            window.SwitchView(true);
        }

        // Hierarchy Context Menus
        [MenuItem("GameObject/ND Menu/添加开关 (Toggle)", false, 10)]
        private static void AddToggleContext(MenuCommand menuCommand)
        {
            if (menuCommand.context is GameObject go)
            {
                var toggle = go.AddComponent<NDToggleItem>();
                toggle.MenuName = go.name;
                toggle.ParameterName = "Toggle_" + go.name.Replace(" ", "_");
                toggle.objectTargets.Add(new GameObjectToggleTarget { targetObject = go, activeWhenOn = true });
                Undo.RegisterCreatedObjectUndo(toggle, "Add ND Toggle Item");
                Selection.activeGameObject = go;
            }
        }

        [MenuItem("GameObject/ND Menu/添加互斥组 (Group)", false, 11)]
        private static void AddGroupContext(MenuCommand menuCommand)
        {
            if (menuCommand.context is GameObject go)
            {
                var group = go.AddComponent<NDToggleGroup>();
                group.MenuName = go.name;
                Undo.RegisterCreatedObjectUndo(group, "Add ND Toggle Group");
                Selection.activeGameObject = go;
            }
        }

        [MenuItem("GameObject/ND Menu/添加滑条 (Radial)", false, 12)]
        private static void AddRadialContext(MenuCommand menuCommand)
        {
            if (menuCommand.context is GameObject go)
            {
                var radial = go.AddComponent<NDRadialPuppet>();
                radial.MenuName = go.name;
                Undo.RegisterCreatedObjectUndo(radial, "Add ND Radial Puppet");
                Selection.activeGameObject = go;
            }
        }

        [MenuItem("GameObject/ND Menu/创建子菜单 (Folder)", false, 13)]
        private static void AddSubMenuContext(MenuCommand menuCommand)
        {
            if (menuCommand.context is GameObject go)
            {
                var subMenu = go.AddComponent<NDSubMenu>();
                subMenu.MenuName = go.name;
                Undo.RegisterCreatedObjectUndo(subMenu, "Add ND SubMenu");
                Selection.activeGameObject = go;
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                currentAvatar = null;
                AutoDetectAvatar();
                RefreshAll();
                ExpandAllTreeItems();
            }
        }

        private void OnHierarchyChange()
        {
            if (currentAvatar == null || currentAvatar.gameObject == null) AutoDetectAvatar();
            RefreshAll();
        }

        private void OnUndoRedoPerformed()
        {
            if (currentAvatar == null || currentAvatar.gameObject == null) AutoDetectAvatar();
            RefreshAll();
        }

        private void OnHierarchyChanged()
        {
            if (currentAvatar == null || currentAvatar.gameObject == null) AutoDetectAvatar();
            RefreshAll();
        }

        private VRCAvatarDescriptor currentAvatar;
        private List<MonoBehaviour> allComponents = new List<MonoBehaviour>();
        private MonoBehaviour currentSelectedComponent;

        // UI references - Common Header & Footer
        private ObjectField avatarField;
        private Button viewWizardBtn;
        private Button viewManagerBtn;
        private VisualElement wizardViewContainer;
        private TwoPaneSplitView managerViewContainer;
        private VisualElement progressFill;
        private Label budgetStatusLabel;
        private Button playModeBtn;
        private Button manualBakeBtn;

        // UI references - Manager View
        private TreeView itemTreeView;
        private VisualElement leftEmptyContainer;
        private Label itemCountLabel;
        private VisualElement emptyState;
        private VisualElement inspectorContainer;

        // UI references - Wizard View
        private WizardMode currentWizardMode = WizardMode.Exclusive;
        private List<WizardItem> wizardItems = new List<WizardItem>();
        private Button wzTabExclusive;
        private Button wzTabProps;
        private TextField wzFolderNameField;
        private Button wzPresetWardrobe;
        private Button wzPresetHair;
        private Button wzPresetBody;
        private Button wzPresetProps;
        private VisualElement wzDropArea;
        private ScrollView wzDroppedItemsList;
        private Toggle wzAutoSubToggles;
        private Toggle wzAutoMutualExclusive;
        private Toggle wzPreventNudityToggle;
        private Toggle wzAutoThumbnailToggle;
        private VisualElement wzOutfitOptionsCard;
        private Button wzGenerateBtn;

        public void CreateGUI()
        {
            // 1. Load Visual Tree
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.custom.nd-menu-manager/Editor/UI/MenuManagerWindow.uxml"
            );

            if (visualTree == null)
            {
                var guids = AssetDatabase.FindAssets("MenuManagerWindow t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                }
            }

            if (visualTree != null)
            {
                visualTree.CloneTree(rootVisualElement);
            }
            else
            {
                rootVisualElement.Add(new Label("ND Menu Manager (UXML not found)"));
                return;
            }

            // 2. Load and attach StyleSheet
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.custom.nd-menu-manager/Editor/UI/MenuManagerWindow.uss"
            );
            if (styleSheet == null)
            {
                var guids = AssetDatabase.FindAssets("MenuManagerWindow t:StyleSheet");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }
            }
            if (styleSheet != null && !rootVisualElement.styleSheets.Contains(styleSheet))
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            // 3. Query Top & Footer UI
            avatarField = rootVisualElement.Q<ObjectField>("avatarSelector");
            viewWizardBtn = rootVisualElement.Q<Button>("viewWizardBtn");
            viewManagerBtn = rootVisualElement.Q<Button>("viewManagerBtn");
            wizardViewContainer = rootVisualElement.Q<VisualElement>("wizardViewContainer");
            managerViewContainer = rootVisualElement.Q<TwoPaneSplitView>("managerViewContainer");

            playModeBtn = rootVisualElement.Q<Button>("playModeBtn");
            manualBakeBtn = rootVisualElement.Q<Button>("manualBakeBtn");
            var refreshBtn = rootVisualElement.Q<Button>("refreshBtn");

            progressFill = rootVisualElement.Q<VisualElement>("progressFill");
            budgetStatusLabel = rootVisualElement.Q<Label>("budgetStatusLabel");

            // Setup View Switching Tabs
            viewWizardBtn?.RegisterCallback<ClickEvent>(_ => SwitchView(true));
            viewManagerBtn?.RegisterCallback<ClickEvent>(_ => SwitchView(false));

            // Setup Manager View UI
            SetupManagerViewElements();

            // Setup Wizard View UI
            SetupWizardViewElements();

            // Setup Header Actions
            SetupHeaderActions(refreshBtn);

            // Auto-detect Avatar
            AutoDetectAvatar();

            // Refresh both views
            RefreshAll();
        }

        private void SwitchView(bool isWizard)
        {
            if (wizardViewContainer == null || managerViewContainer == null) return;

            if (isWizard)
            {
                wizardViewContainer.style.display = DisplayStyle.Flex;
                managerViewContainer.style.display = DisplayStyle.None;
                viewWizardBtn?.AddToClassList("view-switch-active");
                viewManagerBtn?.RemoveFromClassList("view-switch-active");
            }
            else
            {
                wizardViewContainer.style.display = DisplayStyle.None;
                managerViewContainer.style.display = DisplayStyle.Flex;
                viewManagerBtn?.AddToClassList("view-switch-active");
                viewWizardBtn?.RemoveFromClassList("view-switch-active");
                ExpandAllTreeItems();
            }
        }

        #region Setup Views

        private void SetupManagerViewElements()
        {
            itemTreeView = rootVisualElement.Q<TreeView>("menuItemTree");
            if (itemTreeView != null)
            {
                itemTreeView.fixedItemHeight = 44f;
            }

            leftEmptyContainer = rootVisualElement.Q<VisualElement>("leftEmptyContainer");
            itemCountLabel = rootVisualElement.Q<Label>("itemCountLabel");
            emptyState = rootVisualElement.Q<VisualElement>("emptyStateContainer");
            inspectorContainer = rootVisualElement.Q<VisualElement>("inspectorContainer");

            var expandAllBtn = rootVisualElement.Q<Button>("expandAllBtn");
            var collapseAllBtn = rootVisualElement.Q<Button>("collapseAllBtn");
            expandAllBtn?.RegisterCallback<ClickEvent>(_ => ExpandAllTreeItems());
            collapseAllBtn?.RegisterCallback<ClickEvent>(_ => CollapseAllTreeItems());

            var addToggleBtn = rootVisualElement.Q<Button>("addToggleBtn");
            var addGroupBtn = rootVisualElement.Q<Button>("addGroupBtn");
            var addRadialBtn = rootVisualElement.Q<Button>("addRadialBtn");
            var addSubMenuBtn = rootVisualElement.Q<Button>("addSubMenuBtn");

            addToggleBtn?.RegisterCallback<ClickEvent>(_ => AddComponentToSelection<NDToggleItem>());
            addGroupBtn?.RegisterCallback<ClickEvent>(_ => AddComponentToSelection<NDToggleGroup>());
            addRadialBtn?.RegisterCallback<ClickEvent>(_ => AddComponentToSelection<NDRadialPuppet>());
            addSubMenuBtn?.RegisterCallback<ClickEvent>(_ => AddComponentToSelection<NDSubMenu>());

            // Empty state quick create buttons
            rootVisualElement.Q<Button>("quickAddToggleBtn")?.RegisterCallback<ClickEvent>(_ => AddComponentToSelection<NDToggleItem>());
            rootVisualElement.Q<Button>("quickAddGroupBtn")?.RegisterCallback<ClickEvent>(_ => AddComponentToSelection<NDToggleGroup>());
            rootVisualElement.Q<Button>("quickAddRadialBtn")?.RegisterCallback<ClickEvent>(_ => AddComponentToSelection<NDRadialPuppet>());
            rootVisualElement.Q<Button>("quickAddSubMenuBtn")?.RegisterCallback<ClickEvent>(_ => AddComponentToSelection<NDSubMenu>());

            // High performance native double-click expand/collapse
            if (itemTreeView != null)
            {
                itemTreeView.itemsChosen += items =>
                {
                    foreach (var obj in items)
                    {
                        if (obj is MenuItemNode node)
                        {
                            int id = node.id;
                            if (itemTreeView.IsExpanded(id))
                            {
                                itemTreeView.CollapseItem(id, false);
                            }
                            else
                            {
                                itemTreeView.ExpandItem(id, false);
                            }
                        }
                    }
                };
            }
        }

        private void SetupWizardViewElements()
        {
            wzTabExclusive = rootVisualElement.Q<Button>("wzTabExclusive");
            wzTabProps = rootVisualElement.Q<Button>("wzTabProps");

            wzFolderNameField = rootVisualElement.Q<TextField>("wzFolderNameField");
            wzPresetWardrobe = rootVisualElement.Q<Button>("wzPresetWardrobe");
            wzPresetHair = rootVisualElement.Q<Button>("wzPresetHair");
            wzPresetBody = rootVisualElement.Q<Button>("wzPresetBody");
            wzPresetProps = rootVisualElement.Q<Button>("wzPresetProps");

            wzDropArea = rootVisualElement.Q<VisualElement>("wzDropArea");
            wzDroppedItemsList = rootVisualElement.Q<ScrollView>("wzDroppedItemsList");

            wzAutoSubToggles = rootVisualElement.Q<Toggle>("wzAutoSubToggles");
            wzAutoMutualExclusive = rootVisualElement.Q<Toggle>("wzAutoMutualExclusive");
            wzPreventNudityToggle = rootVisualElement.Q<Toggle>("wzPreventNudityToggle");
            wzAutoThumbnailToggle = rootVisualElement.Q<Toggle>("wzAutoThumbnailToggle");
            wzOutfitOptionsCard = rootVisualElement.Q<VisualElement>("wzOutfitOptionsCard");
            wzGenerateBtn = rootVisualElement.Q<Button>("wzGenerateBtn");

            // Setup Mode Switch Tabs
            wzTabExclusive?.RegisterCallback<ClickEvent>(_ => SetWizardMode(WizardMode.Exclusive));
            wzTabProps?.RegisterCallback<ClickEvent>(_ => SetWizardMode(WizardMode.Props));

            // Setup Presets
            wzPresetWardrobe?.RegisterCallback<ClickEvent>(_ => { if (wzFolderNameField != null) wzFolderNameField.value = "衣柜"; SetWizardMode(WizardMode.Exclusive); });
            wzPresetHair?.RegisterCallback<ClickEvent>(_ => { if (wzFolderNameField != null) wzFolderNameField.value = "发型"; SetWizardMode(WizardMode.Exclusive); });
            wzPresetBody?.RegisterCallback<ClickEvent>(_ => { if (wzFolderNameField != null) wzFolderNameField.value = "体型"; SetWizardMode(WizardMode.Exclusive); });
            wzPresetProps?.RegisterCallback<ClickEvent>(_ => { if (wzFolderNameField != null) wzFolderNameField.value = "饰品"; SetWizardMode(WizardMode.Props); });

            // Setup Drop Area
            SetupWizardDropArea();

            // Setup Generate Button
            wzGenerateBtn?.RegisterCallback<ClickEvent>(_ => ExecuteWizardGeneration());
        }

        private void SetupHeaderActions(Button refreshBtn)
        {
            if (avatarField != null)
            {
                avatarField.objectType = typeof(GameObject);
                avatarField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue is GameObject go && go != null)
                    {
                        currentAvatar = go.GetComponent<VRCAvatarDescriptor>() ??
                                        go.GetComponentInParent<VRCAvatarDescriptor>() ??
                                        go.GetComponentInChildren<VRCAvatarDescriptor>();
                    }
                    else
                    {
                        currentAvatar = null;
                    }
                    RefreshAll();
                });
            }

            refreshBtn?.RegisterCallback<ClickEvent>(_ =>
            {
                AutoDetectAvatar();
                RefreshAll();
                ExpandAllTreeItems();
            });
            playModeBtn?.RegisterCallback<ClickEvent>(_ => TogglePlayMode());
            manualBakeBtn?.RegisterCallback<ClickEvent>(_ => ManualBakeAssets());
        }

        private void SetWizardMode(WizardMode mode)
        {
            currentWizardMode = mode;

            wzTabExclusive?.RemoveFromClassList("tab-active");
            wzTabProps?.RemoveFromClassList("tab-active");

            switch (mode)
            {
                case WizardMode.Exclusive:
                    wzTabExclusive?.AddToClassList("tab-active");
                    if (wzFolderNameField != null && string.IsNullOrEmpty(wzFolderNameField.value)) wzFolderNameField.value = "衣柜";
                    if (wzOutfitOptionsCard != null) wzOutfitOptionsCard.style.display = DisplayStyle.Flex;
                    break;
                case WizardMode.Props:
                    wzTabProps?.AddToClassList("tab-active");
                    if (wzFolderNameField != null && (wzFolderNameField.value == "衣柜" || string.IsNullOrEmpty(wzFolderNameField.value))) wzFolderNameField.value = "饰品";
                    if (wzOutfitOptionsCard != null) wzOutfitOptionsCard.style.display = DisplayStyle.None;
                    break;
            }
        }

        #endregion

        #region Wizard Logic

        private void SetupWizardDropArea()
        {
            if (wzDropArea == null) return;

            wzDropArea.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DragAndDrop.objectReferences.Length > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
            });

            wzDropArea.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go)
                    {
                        if (currentAvatar == null)
                        {
                            var desc = go.GetComponentInParent<VRCAvatarDescriptor>() ?? go.GetComponentInChildren<VRCAvatarDescriptor>();
                            if (desc != null)
                            {
                                currentAvatar = desc;
                                if (avatarField != null) avatarField.value = desc.gameObject;
                            }
                        }

                        if (!wizardItems.Exists(x => x.gameObject == go))
                        {
                            wizardItems.Add(new WizardItem
                            {
                                gameObject = go,
                                displayName = go.name
                            });
                        }
                    }
                }

                UpdateWizardDroppedList();
            });
        }

        private void UpdateWizardDroppedList()
        {
            if (wzDroppedItemsList == null) return;

            wzDroppedItemsList.Clear();

            for (int i = 0; i < wizardItems.Count; i++)
            {
                int index = i;
                var item = wizardItems[i];
                if (item == null || item.gameObject == null) continue;

                var row = new VisualElement();
                row.AddToClassList("item-row");

                // Badge
                var badge = new Label(index == 0 ? "#1 (默认)" : $"#{index + 1}");
                badge.AddToClassList("item-index-badge");
                if (index == 0) badge.AddToClassList("item-index-default");

                // Move Up Button
                var upBtn = new Button(() =>
                {
                    if (index > 0)
                    {
                        var temp = wizardItems[index];
                        wizardItems[index] = wizardItems[index - 1];
                        wizardItems[index - 1] = temp;
                        UpdateWizardDroppedList();
                    }
                })
                {
                    text = "▲"
                };
                upBtn.AddToClassList("item-order-btn");
                if (index == 0) upBtn.SetEnabled(false);

                // Move Down Button
                var downBtn = new Button(() =>
                {
                    if (index < wizardItems.Count - 1)
                    {
                        var temp = wizardItems[index];
                        wizardItems[index] = wizardItems[index + 1];
                        wizardItems[index + 1] = temp;
                        UpdateWizardDroppedList();
                    }
                })
                {
                    text = "▼"
                };
                downBtn.AddToClassList("item-order-btn");
                if (index == wizardItems.Count - 1) downBtn.SetEnabled(false);

                // Editable Display Name Field
                var nameInput = new TextField();
                nameInput.value = item.displayName;
                nameInput.AddToClassList("item-name-input");
                nameInput.RegisterValueChangedCallback(evt =>
                {
                    item.displayName = string.IsNullOrEmpty(evt.newValue) ? item.gameObject.name : evt.newValue;
                });

                // Source Object Label
                var sourceLabel = new Label($"({item.gameObject.name})");
                sourceLabel.AddToClassList("item-source-label");

                // Delete Button
                var delBtn = new Button(() =>
                {
                    wizardItems.RemoveAt(index);
                    UpdateWizardDroppedList();
                })
                {
                    text = "x"
                };
                delBtn.AddToClassList("item-del-btn");

                row.Add(badge);
                row.Add(upBtn);
                row.Add(downBtn);
                row.Add(nameInput);
                row.Add(sourceLabel);
                row.Add(delBtn);

                wzDroppedItemsList.Add(row);
            }
        }

        private void ExecuteWizardGeneration()
        {
            if (currentAvatar == null)
            {
                EditorUtility.DisplayDialog("提示", "请先在上方选择目标 Avatar 模型！", "确定");
                return;
            }

            if (wizardItems.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先从 Hierarchy 中拖入至少一个衣服或发型物体！", "确定");
                return;
            }

            string folderName = string.IsNullOrEmpty(wzFolderNameField?.value) ? "菜单" : wzFolderNameField.value;

            switch (currentWizardMode)
            {
                case WizardMode.Exclusive:
                    GenerateExclusiveStructure(folderName);
                    break;
                case WizardMode.Props:
                    GeneratePropsStructure(folderName);
                    break;
            }

            // Refresh & switch directly to Manager View so user sees the result immediately!
            RefreshAll();
            SwitchView(false);

            EditorUtility.DisplayDialog("生成成功", $"已成功生成【{folderName}】完整菜单体系与缩略图！\n已自动为您切换至【菜单层级管理】面板，可即时预览与微调。", "确定");
        }

        private void GenerateExclusiveStructure(string rootFolderName)
        {
            bool scanSubToggles = wzAutoSubToggles == null || wzAutoSubToggles.value;
            bool mutualExclusive = wzAutoMutualExclusive == null || wzAutoMutualExclusive.value;
            bool preventNudity = wzPreventNudityToggle == null || wzPreventNudityToggle.value;
            bool genThumbnails = wzAutoThumbnailToggle == null || wzAutoThumbnailToggle.value;
            string avatarName = currentAvatar.gameObject.name;

            // 1. Create main folder
            var rootGo = new GameObject(rootFolderName);
            rootGo.transform.SetParent(currentAvatar.transform, false);
            var rootSubMenu = rootGo.AddComponent<NDSubMenu>();
            rootSubMenu.MenuName = rootFolderName;
            Undo.RegisterCreatedObjectUndo(rootGo, "Generate Exclusive Structure");

            string sharedParamName = $"{rootFolderName}_Select".Replace(" ", "_");

            // 2. For each item
            for (int i = 0; i < wizardItems.Count; i++)
            {
                var item = wizardItems[i];
                var targetGo = item.gameObject;
                string itemDisplayName = string.IsNullOrEmpty(item.displayName) ? targetGo.name : item.displayName;

                Texture2D itemIcon = null;
                if (genThumbnails)
                {
                    var raw = ThumbnailGenerator.CaptureGameObjectThumbnail(targetGo);
                    itemIcon = ThumbnailGenerator.SaveThumbnailAsset(raw, avatarName, itemDisplayName);
                }

                // Create Item SubMenu Folder
                var itemSubGo = new GameObject(itemDisplayName);
                itemSubGo.transform.SetParent(rootGo.transform, false);
                var itemSubMenu = itemSubGo.AddComponent<NDSubMenu>();
                itemSubMenu.MenuName = itemDisplayName;
                itemSubMenu.Icon = itemIcon;

                // Create Main Switch for this item
                string switchPrefix = (rootFolderName.Contains("发") || rootFolderName.Contains("头")) ? "切换至" : "穿上";
                var mainToggleGo = new GameObject($"{switchPrefix}_{itemDisplayName}");
                mainToggleGo.transform.SetParent(itemSubGo.transform, false);
                var mainToggle = mainToggleGo.AddComponent<NDToggleItem>();
                mainToggle.MenuName = $"{switchPrefix} {itemDisplayName}";
                mainToggle.Icon = itemIcon;
                mainToggle.UseIntParameter = mutualExclusive;
                mainToggle.ParameterName = mutualExclusive ? sharedParamName : $"Toggle_{rootFolderName}_{itemDisplayName.Replace(" ", "_")}";
                mainToggle.ParameterValue = i + 1;
                mainToggle.DefaultValue = (i == 0);
                mainToggle.AllowAllOff = !preventNudity;

                // Target: Turn ON this item
                mainToggle.objectTargets.Add(new GameObjectToggleTarget
                {
                    targetObject = targetGo,
                    activeWhenOn = true
                });

                // Targets: Turn OFF other items
                if (mutualExclusive)
                {
                    for (int j = 0; j < wizardItems.Count; j++)
                    {
                        if (i == j) continue;
                        var otherGo = wizardItems[j].gameObject;
                        if (otherGo != null)
                        {
                            mainToggle.objectTargets.Add(new GameObjectToggleTarget
                            {
                                targetObject = otherGo,
                                activeWhenOn = false
                            });
                        }
                    }
                }

                // 3. Scan children of targetGo for sub-toggles
                if (scanSubToggles && targetGo.transform.childCount > 0)
                {
                    foreach (Transform child in targetGo.transform)
                    {
                        if (!IsValidSubMeshItem(child)) continue;

                        Texture2D childIcon = null;
                        if (genThumbnails)
                        {
                            var childRaw = ThumbnailGenerator.CaptureGameObjectThumbnail(child.gameObject);
                            childIcon = ThumbnailGenerator.SaveThumbnailAsset(childRaw, avatarName, $"{itemDisplayName}_{child.name}");
                        }

                        var childToggleGo = new GameObject(child.name);
                        childToggleGo.transform.SetParent(itemSubGo.transform, false);
                        var childToggle = childToggleGo.AddComponent<NDToggleItem>();
                        childToggle.MenuName = child.name;
                        childToggle.Icon = childIcon;
                        childToggle.ParameterName = $"Toggle_{itemDisplayName}_{child.name}".Replace(" ", "_");
                        childToggle.DefaultValue = child.gameObject.activeSelf;
                        childToggle.objectTargets.Add(new GameObjectToggleTarget
                        {
                            targetObject = child.gameObject,
                            activeWhenOn = true
                        });
                    }
                }
            }
        }

        private void GeneratePropsStructure(string folderName)
        {
            bool genThumbnails = wzAutoThumbnailToggle == null || wzAutoThumbnailToggle.value;
            string avatarName = currentAvatar.gameObject.name;

            var rootGo = new GameObject(folderName);
            rootGo.transform.SetParent(currentAvatar.transform, false);
            var rootSubMenu = rootGo.AddComponent<NDSubMenu>();
            rootSubMenu.MenuName = folderName;
            Undo.RegisterCreatedObjectUndo(rootGo, "Generate Props Structure");

            foreach (var item in wizardItems)
            {
                var prop = item.gameObject;
                string propDisplayName = string.IsNullOrEmpty(item.displayName) ? prop.name : item.displayName;

                Texture2D propIcon = null;
                if (genThumbnails)
                {
                    var raw = ThumbnailGenerator.CaptureGameObjectThumbnail(prop);
                    propIcon = ThumbnailGenerator.SaveThumbnailAsset(raw, avatarName, $"Prop_{propDisplayName}");
                }

                var toggleGo = new GameObject(propDisplayName);
                toggleGo.transform.SetParent(rootGo.transform, false);
                var toggle = toggleGo.AddComponent<NDToggleItem>();
                toggle.MenuName = propDisplayName;
                toggle.Icon = propIcon;
                toggle.ParameterName = "Toggle_" + propDisplayName.Replace(" ", "_");
                toggle.DefaultValue = prop.activeSelf;
                toggle.objectTargets.Add(new GameObjectToggleTarget
                {
                    targetObject = prop,
                    activeWhenOn = true
                });
            }
        }

        private static bool IsValidSubMeshItem(Transform child)
        {
            if (child == null) return false;

            if (child.TryGetComponent<SkinnedMeshRenderer>(out var smr))
            {
                return smr.sharedMesh != null && smr.sharedMesh.vertexCount > 0;
            }

            if (child.TryGetComponent<MeshRenderer>(out _) && child.TryGetComponent<MeshFilter>(out var mf))
            {
                return mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0;
            }

            var smrs = child.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in smrs)
            {
                if (s.sharedMesh != null && s.sharedMesh.vertexCount > 0)
                {
                    return true;
                }
            }

            var mfs = child.GetComponentsInChildren<MeshFilter>(true);
            foreach (var m in mfs)
            {
                if (m.sharedMesh != null && m.sharedMesh.vertexCount > 0 && m.GetComponent<MeshRenderer>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Manager & Inspector Logic

        private void AutoDetectAvatar()
        {
            // 1. If avatarField has a valid GameObject in the scene, prioritize it
            if (avatarField != null && avatarField.value is GameObject fieldGo && fieldGo != null)
            {
                var desc = fieldGo.GetComponent<VRCAvatarDescriptor>() ??
                           fieldGo.GetComponentInParent<VRCAvatarDescriptor>() ??
                           fieldGo.GetComponentInChildren<VRCAvatarDescriptor>();
                if (desc != null)
                {
                    currentAvatar = desc;
                    return;
                }
            }

            // 2. Try Selection
            if (Selection.activeGameObject != null)
            {
                var desc = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>() ??
                           Selection.activeGameObject.GetComponentInChildren<VRCAvatarDescriptor>();
                if (desc != null)
                {
                    currentAvatar = desc;
                    if (avatarField != null) avatarField.SetValueWithoutNotify(desc.gameObject);
                    return;
                }
            }

            // 3. Find any active descriptor in scene
            var sceneDescriptor = UnityEngine.Object.FindObjectOfType<VRCAvatarDescriptor>();
            if (sceneDescriptor != null)
            {
                currentAvatar = sceneDescriptor;
                if (avatarField != null) avatarField.SetValueWithoutNotify(sceneDescriptor.gameObject);
            }
            else
            {
                currentAvatar = null;
                if (avatarField != null) avatarField.SetValueWithoutNotify(null);
            }
        }

        public void RefreshAll()
        {
            if (currentAvatar == null || currentAvatar.gameObject == null)
            {
                AutoDetectAvatar();
            }

            ScanAvatarComponents();
            BuildTree();
            RecalculateBudget();
            UpdateWizardDroppedList();

            if (currentSelectedComponent != null && allComponents.Contains(currentSelectedComponent))
            {
                ShowItemInspector(currentSelectedComponent);
            }
            else
            {
                HideItemInspector();
            }
        }

        private void ScanAvatarComponents()
        {
            allComponents.Clear();
            if (currentAvatar == null) return;

            var toggles = currentAvatar.GetComponentsInChildren<NDToggleItem>(true);
            var groups = currentAvatar.GetComponentsInChildren<NDToggleGroup>(true);
            var radials = currentAvatar.GetComponentsInChildren<NDRadialPuppet>(true);
            var subMenus = currentAvatar.GetComponentsInChildren<NDSubMenu>(true);

            allComponents.AddRange(toggles);
            allComponents.AddRange(groups);
            allComponents.AddRange(radials);
            allComponents.AddRange(subMenus);
        }

        private void BuildTree()
        {
            if (itemTreeView == null) return;

            if (currentAvatar == null || allComponents.Count == 0)
            {
                itemTreeView.style.display = DisplayStyle.None;
                leftEmptyContainer.style.display = DisplayStyle.Flex;
                if (itemCountLabel != null) itemCountLabel.text = "0 项";
                HideItemInspector();
                return;
            }

            leftEmptyContainer.style.display = DisplayStyle.None;
            itemTreeView.style.display = DisplayStyle.Flex;

            int visibleCount = 0;
            var rootNodes = new List<TreeViewItemData<MenuItemNode>>();
            var visitedTransforms = new HashSet<Transform>();

            foreach (var comp in allComponents.ToArray())
            {
                if (comp == null) continue;
                var trans = comp.transform;

                var parentComp = trans.parent != null ? trans.parent.GetComponentInParent<INDMenuItem>() as MonoBehaviour : null;
                if (parentComp == null || !allComponents.Contains(parentComp))
                {
                    var node = CreateTreeItemDataRecursive(comp, ref visibleCount, visitedTransforms);
                    if (node.id != -1)
                    {
                        rootNodes.Add(node);
                    }
                }
            }

            if (itemCountLabel != null) itemCountLabel.text = $"{visibleCount} 项";

            itemTreeView.fixedItemHeight = 44f;
            itemTreeView.SetRootItems(rootNodes);
            itemTreeView.Rebuild();
            itemTreeView.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("tree-row");

                var iconBox = new VisualElement();
                iconBox.AddToClassList("row-icon-container");

                var iconElem = new Image();
                iconElem.name = "rowIcon";
                iconElem.AddToClassList("row-icon-img");
                iconBox.Add(iconElem);

                var badge = new Label();
                badge.name = "rowBadge";
                badge.AddToClassList("tree-badge");

                var textGroup = new VisualElement();
                textGroup.AddToClassList("row-text-group");

                var title = new Label();
                title.name = "rowTitle";
                title.AddToClassList("row-title");

                var subtitle = new Label();
                subtitle.name = "rowSubtitle";
                subtitle.AddToClassList("row-subtitle");

                textGroup.Add(title);
                textGroup.Add(subtitle);

                row.Add(iconBox);
                row.Add(badge);
                row.Add(textGroup);

                // --- Register Event Handlers ONCE per pooled row (prevents accumulation) ---
                void ClearDropClasses()
                {
                    row.RemoveFromClassList("drop-target-above");
                    row.RemoveFromClassList("drop-target-below");
                    row.RemoveFromClassList("drop-target-inside");
                }

                Vector2 pointerDownPos = Vector2.zero;
                bool isPointerDown = false;

                row.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0)
                    {
                        isPointerDown = true;
                        pointerDownPos = evt.mousePosition;
                    }
                });

                row.RegisterCallback<MouseUpEvent>(evt =>
                {
                    isPointerDown = false;
                    row.RemoveFromClassList("is-dragging");
                    itemTreeView?.RemoveFromClassList("dragging-in-progress");
                });

                row.RegisterCallback<MouseMoveEvent>(evt =>
                {
                    if (isPointerDown && evt.pressedButtons == 1 && row.userData is MenuItemNode node && node.component != null)
                    {
                        if ((evt.mousePosition - pointerDownPos).sqrMagnitude > 16f)
                        {
                            isPointerDown = false;
                            row.AddToClassList("is-dragging");
                            itemTreeView?.AddToClassList("dragging-in-progress");

                            DragAndDrop.PrepareStartDrag();
                            DragAndDrop.SetGenericData("NDDragNode", node);
                            DragAndDrop.objectReferences = new UnityEngine.Object[] { node.component.gameObject };
                            DragAndDrop.StartDrag(node.name);
                            evt.StopPropagation();
                        }
                    }
                });

                row.RegisterCallback<DragUpdatedEvent>(evt =>
                {
                    var dragNode = DragAndDrop.GetGenericData("NDDragNode") as MenuItemNode;
                    var targetNode = row.userData as MenuItemNode;

                    if (dragNode != null && dragNode.component != null && targetNode != null && targetNode.component != null)
                    {
                        if (dragNode.component == targetNode.component || targetNode.component.transform.IsChildOf(dragNode.component.transform))
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                            ClearDropClasses();
                        }
                        else
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                            ClearDropClasses();

                            float rowH = row.layout.height > 0 ? row.layout.height : 40f;
                            float localY = evt.localMousePosition.y;

                            if (targetNode.component is NDSubMenu)
                            {
                                if (localY < rowH * 0.25f)
                                {
                                    row.AddToClassList("drop-target-above");
                                }
                                else if (localY > rowH * 0.75f)
                                {
                                    row.AddToClassList("drop-target-below");
                                }
                                else
                                {
                                    row.AddToClassList("drop-target-inside");
                                }
                            }
                            else
                            {
                                if (localY < rowH * 0.5f)
                                {
                                    row.AddToClassList("drop-target-above");
                                }
                                else
                                {
                                    row.AddToClassList("drop-target-below");
                                }
                            }
                        }
                        evt.StopPropagation();
                    }
                });

                row.RegisterCallback<DragLeaveEvent>(evt =>
                {
                    ClearDropClasses();
                });

                row.RegisterCallback<DragExitedEvent>(evt =>
                {
                    ClearDropClasses();
                    row.RemoveFromClassList("is-dragging");
                    itemTreeView?.RemoveFromClassList("dragging-in-progress");
                });

                row.RegisterCallback<DragPerformEvent>(evt =>
                {
                    bool isAbove = row.ClassListContains("drop-target-above");
                    bool isInside = row.ClassListContains("drop-target-inside");
                    ClearDropClasses();
                    row.RemoveFromClassList("is-dragging");
                    itemTreeView?.RemoveFromClassList("dragging-in-progress");

                    var dragNode = DragAndDrop.GetGenericData("NDDragNode") as MenuItemNode;
                    var targetNode = row.userData as MenuItemNode;

                    if (dragNode != null && dragNode.component != null && targetNode != null && targetNode.component != null)
                    {
                        if (dragNode.component != targetNode.component && !targetNode.component.transform.IsChildOf(dragNode.component.transform))
                        {
                            DragAndDrop.AcceptDrag();

                            Transform sourceTrans = dragNode.component.transform;
                            Transform targetTrans = targetNode.component.transform;

                            if (isInside && targetNode.component is NDSubMenu)
                            {
                                Undo.SetTransformParent(sourceTrans, targetTrans, "Move Menu Item into Folder");
                                sourceTrans.SetAsLastSibling();
                            }
                            else if (isAbove)
                            {
                                Undo.SetTransformParent(sourceTrans, targetTrans.parent, "Insert Menu Item Above");
                                int targetIdx = targetTrans.GetSiblingIndex();
                                sourceTrans.SetSiblingIndex(targetIdx);
                            }
                            else
                            {
                                Undo.SetTransformParent(sourceTrans, targetTrans.parent, "Insert Menu Item Below");
                                int targetIdx = targetTrans.GetSiblingIndex();
                                sourceTrans.SetSiblingIndex(targetIdx + 1);
                            }

                            RefreshAll();
                        }
                        evt.StopPropagation();
                    }
                });

                return row;
            };

            itemTreeView.bindItem = (element, index) =>
            {
                var item = itemTreeView.GetItemDataForIndex<MenuItemNode>(index);
                if (item == null) return;

                element.userData = item;

                var title = element.Q<Label>("rowTitle");
                var subtitle = element.Q<Label>("rowSubtitle");
                var badge = element.Q<Label>("rowBadge");
                var iconElem = element.Q<Image>("rowIcon");

                if (title != null) title.text = item.name;
                if (subtitle != null) subtitle.text = string.IsNullOrEmpty(item.paramName) ? $"[{item.itemType}]" : item.paramName;
                if (iconElem != null)
                {
                    iconElem.image = item.icon != null ? item.icon : EditorGUIUtility.IconContent("GameObject Icon").image;
                }

                if (badge != null)
                {
                    badge.RemoveFromClassList("badge-toggle");
                    badge.RemoveFromClassList("badge-group");
                    badge.RemoveFromClassList("badge-radial");
                    badge.RemoveFromClassList("badge-folder");

                    switch (item.itemType)
                    {
                        case MenuItemType.Toggle:
                            badge.text = $"开关 {item.bitCost}b";
                            badge.AddToClassList("badge-toggle");
                            break;
                        case MenuItemType.ToggleGroup:
                            badge.text = $"互斥 {item.bitCost}b";
                            badge.AddToClassList("badge-group");
                            break;
                        case MenuItemType.RadialPuppet:
                            badge.text = $"滑条 {item.bitCost}b";
                            badge.AddToClassList("badge-radial");
                            break;
                        case MenuItemType.SubMenu:
                            badge.text = "目录";
                            badge.AddToClassList("badge-folder");
                            break;
                    }
                }

            };

            itemTreeView.selectedIndicesChanged += indices =>
            {
                foreach (var idx in indices)
                {
                    var selected = itemTreeView.GetItemDataForIndex<MenuItemNode>(idx);
                    if (selected != null && selected.component != null)
                    {
                        currentSelectedComponent = selected.component;
                        Selection.activeGameObject = selected.component.gameObject;
                        ShowItemInspector(selected.component);
                        return;
                    }
                }
                currentSelectedComponent = null;
                HideItemInspector();
            };

            itemTreeView.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var dragNode = DragAndDrop.GetGenericData("NDDragNode") as MenuItemNode;
                if (dragNode != null && dragNode.component != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                }
            });

            itemTreeView.RegisterCallback<DragPerformEvent>(evt =>
            {
                var dragNode = DragAndDrop.GetGenericData("NDDragNode") as MenuItemNode;
                if (dragNode != null && dragNode.component != null && currentAvatar != null)
                {
                    DragAndDrop.AcceptDrag();
                    Undo.SetTransformParent(dragNode.component.transform, currentAvatar.transform, "Move Menu Item to Root");
                    RefreshAll();
                }
            });

            itemTreeView.Rebuild();
            ExpandAllTreeItems();
        }

        private void ExpandAllTreeItems()
        {
            if (itemTreeView == null) return;
            try
            {
                itemTreeView.ExpandAll();
            }
            catch
            {
                foreach (var comp in allComponents)
                {
                    if (comp != null)
                    {
                        itemTreeView.ExpandItem(comp.GetInstanceID(), true);
                    }
                }
            }
        }

        private void CollapseAllTreeItems()
        {
            if (itemTreeView == null) return;
            try
            {
                itemTreeView.CollapseAll();
            }
            catch
            {
                foreach (var comp in allComponents)
                {
                    if (comp != null)
                    {
                        itemTreeView.CollapseItem(comp.GetInstanceID(), true);
                    }
                }
            }
        }

        private TreeViewItemData<MenuItemNode> CreateTreeItemDataRecursive(MonoBehaviour comp, ref int count, HashSet<Transform> visited)
        {
            if (comp == null) return new TreeViewItemData<MenuItemNode>(-1, null);

            var menuItem = comp as INDMenuItem;
            string displayName = (menuItem != null && !string.IsNullOrEmpty(menuItem.MenuName)) ? menuItem.MenuName : comp.gameObject.name;
            string paramName = menuItem != null ? menuItem.ParameterName : "";

            var childNodes = new List<TreeViewItemData<MenuItemNode>>();
            foreach (Transform child in comp.transform)
            {
                var childComp = child.GetComponent<MonoBehaviour>();
                if (childComp != null && childComp is INDMenuItem && allComponents.Contains(childComp))
                {
                    var childNode = CreateTreeItemDataRecursive(childComp, ref count, visited);
                    if (childNode.id != -1)
                    {
                        childNodes.Add(childNode);
                    }
                }
            }

            count++;
            var nodeData = new MenuItemNode
            {
                id = comp.GetInstanceID(),
                component = comp,
                name = displayName,
                paramName = paramName,
                itemType = menuItem != null ? menuItem.ItemType : MenuItemType.Toggle,
                bitCost = menuItem != null ? menuItem.GetBitCost() : 0,
                icon = menuItem != null ? menuItem.Icon : null
            };

            return new TreeViewItemData<MenuItemNode>(nodeData.id, nodeData, childNodes);
        }

        private void ShowItemInspector(MonoBehaviour item)
        {
            if (emptyState == null || inspectorContainer == null || item == null) return;

            currentSelectedComponent = item;
            emptyState.style.display = DisplayStyle.None;
            inspectorContainer.style.display = DisplayStyle.Flex;
            inspectorContainer.Clear();

            var serializedObject = new SerializedObject(item);
            var menuItem = item as INDMenuItem;

            // =========================================================================
            // 1. Hero Banner Card
            // =========================================================================
            var heroCard = new VisualElement();
            heroCard.AddToClassList("hero-banner-card");

            var thumbBox = new VisualElement();
            thumbBox.AddToClassList("hero-thumb-box");
            var thumbImg = new Image();
            thumbImg.AddToClassList("hero-thumb-img");
            thumbImg.image = menuItem?.Icon != null ? menuItem.Icon : EditorGUIUtility.IconContent("GameObject Icon").image;
            thumbBox.Add(thumbImg);
            heroCard.Add(thumbBox);

            var infoGroup = new VisualElement();
            infoGroup.AddToClassList("hero-info-group");

            // Breadcrumb path
            string breadcrumb = BuildBreadcrumbPath(item);
            var breadcrumbLabel = new Label(breadcrumb);
            breadcrumbLabel.AddToClassList("hero-breadcrumb");
            infoGroup.Add(breadcrumbLabel);

            // Title TextField
            var titleField = new TextField();
            titleField.value = menuItem != null ? menuItem.MenuName : item.gameObject.name;
            titleField.AddToClassList("hero-title-input");
            titleField.RegisterValueChangedCallback(evt =>
            {
                if (menuItem != null)
                {
                    menuItem.MenuName = evt.newValue;
                    EditorUtility.SetDirty(item);
                    itemTreeView?.Rebuild();
                }
            });
            infoGroup.Add(titleField);

            // Meta row: Type & Bit Cost
            var metaRow = new VisualElement();
            metaRow.AddToClassList("hero-meta-row");
            var typeBadge = new Label($"组件类型: {menuItem?.ItemType}");
            typeBadge.AddToClassList("badge-pill");
            var bitBadge = new Label($"同步占用: {menuItem?.GetBitCost()} bits");
            bitBadge.AddToClassList("badge-pill");
            metaRow.Add(typeBadge);
            metaRow.Add(bitBadge);
            infoGroup.Add(metaRow);

            heroCard.Add(infoGroup);

            // Actions: Capture 3D Icon, Ping Object, Remove
            var heroActions = new VisualElement();
            heroActions.AddToClassList("hero-actions-group");

            var captureBtn = new Button(() =>
            {
                if (currentAvatar == null)
                {
                    EditorUtility.DisplayDialog("提示", "请先在上方指定 Target Avatar！", "确定");
                    return;
                }

                var raw = ThumbnailGenerator.CaptureGameObjectThumbnail(item.gameObject);
                if (raw == null)
                {
                    EditorUtility.DisplayDialog("提示", "未能获取到该物体的有效网格渲染器（Renderer），请确认物体包含 Mesh！", "确定");
                    return;
                }

                string avatarName = currentAvatar.gameObject.name;
                var icon = ThumbnailGenerator.SaveThumbnailAsset(raw, avatarName, item.gameObject.name);

                if (menuItem != null)
                {
                    menuItem.Icon = icon;
                    EditorUtility.SetDirty(item);
                }

                serializedObject.Update();
                RefreshAll();
                ShowItemInspector(item);
                EditorUtility.DisplayDialog("截取成功", $"已成功为 [{item.gameObject.name}] 生成并关联 3D 透明缩略图！", "确定");
            })
            {
                text = "截取 3D 图标"
            };
            captureBtn.AddToClassList("hero-btn");
            captureBtn.AddToClassList("hero-btn-purple");
            heroActions.Add(captureBtn);

            var pingBtn = new Button(() =>
            {
                EditorGUIUtility.PingObject(item.gameObject);
                Selection.activeGameObject = item.gameObject;
            })
            {
                text = "定位物体"
            };
            pingBtn.AddToClassList("hero-btn");
            pingBtn.AddToClassList("hero-btn-dark");
            heroActions.Add(pingBtn);

            var deleteBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog("删除确认", $"确定要从物体 [{item.gameObject.name}] 上移除该改模组件吗？", "删除", "取消"))
                {
                    Undo.DestroyObjectImmediate(item);
                    RefreshAll();
                    HideItemInspector();
                }
            })
            {
                text = "移除组件"
            };
            deleteBtn.AddToClassList("hero-btn");
            deleteBtn.AddToClassList("hero-btn-danger");
            heroActions.Add(deleteBtn);

            heroCard.Add(heroActions);
            inspectorContainer.Add(heroCard);

            // =========================================================================
            // 2. Card 1: 菜单与网络参数配置 (Menu & Parameter Settings)
            // =========================================================================
            var paramCard = new VisualElement();
            paramCard.AddToClassList("inspector-card");

            var paramHeader = new VisualElement();
            paramHeader.AddToClassList("card-heading-row");
            var paramTitle = new Label("菜单与网络参数配置");
            paramTitle.AddToClassList("card-heading");
            paramHeader.Add(paramTitle);
            paramCard.Add(paramHeader);

            if (item is NDToggleItem toggle)
            {
                var pNameField = new TextField("表达式参数 (Parameter):") { value = toggle.ParameterName };
                pNameField.AddToClassList("form-field");
                pNameField.RegisterValueChangedCallback(evt => { toggle.ParameterName = evt.newValue; EditorUtility.SetDirty(toggle); RecalculateBudget(); itemTreeView?.Rebuild(); });
                paramCard.Add(pNameField);

                var defaultToggle = new Toggle("默认开启状态 (Default On)") { value = toggle.DefaultValue };
                defaultToggle.AddToClassList("form-toggle");
                defaultToggle.RegisterValueChangedCallback(evt => { toggle.DefaultValue = evt.newValue; EditorUtility.SetDirty(toggle); });
                paramCard.Add(defaultToggle);

                var savedToggle = new Toggle("记住状态 (Saved)") { value = toggle.Saved };
                savedToggle.AddToClassList("form-toggle");
                savedToggle.RegisterValueChangedCallback(evt => { toggle.Saved = evt.newValue; EditorUtility.SetDirty(toggle); });
                paramCard.Add(savedToggle);

                var syncedToggle = new Toggle("网络同步 (Synced)") { value = toggle.Synced };
                syncedToggle.AddToClassList("form-toggle");
                syncedToggle.RegisterValueChangedCallback(evt => { toggle.Synced = evt.newValue; EditorUtility.SetDirty(toggle); RecalculateBudget(); itemTreeView?.Rebuild(); });
                paramCard.Add(syncedToggle);

                var useIntToggle = new Toggle("作为多选互斥分支 (Use Int Param)") { value = toggle.UseIntParameter };
                useIntToggle.AddToClassList("form-toggle");
                useIntToggle.RegisterValueChangedCallback(evt => { toggle.UseIntParameter = evt.newValue; EditorUtility.SetDirty(toggle); ShowItemInspector(toggle); });
                paramCard.Add(useIntToggle);

                if (toggle.UseIntParameter)
                {
                    var intValField = new IntegerField("互斥槽位值 (Slot Value):") { value = toggle.ParameterValue };
                    intValField.AddToClassList("form-field");
                    intValField.RegisterValueChangedCallback(evt => { toggle.ParameterValue = evt.newValue; EditorUtility.SetDirty(toggle); });
                    paramCard.Add(intValField);

                    var allowOffToggle = new Toggle("允许完全脱下/全关 (Allow All Off)") { value = toggle.AllowAllOff };
                    allowOffToggle.AddToClassList("form-toggle");
                    allowOffToggle.RegisterValueChangedCallback(evt => { toggle.AllowAllOff = evt.newValue; EditorUtility.SetDirty(toggle); });
                    paramCard.Add(allowOffToggle);
                }
            }
            else if (item is NDToggleGroup group)
            {
                var pNameField = new TextField("表达式参数 (Parameter):") { value = group.ParameterName };
                pNameField.AddToClassList("form-field");
                pNameField.RegisterValueChangedCallback(evt => { group.ParameterName = evt.newValue; EditorUtility.SetDirty(group); RecalculateBudget(); itemTreeView?.Rebuild(); });
                paramCard.Add(pNameField);

                var defaultIdxField = new IntegerField("默认选中项索引 (Default Index):") { value = group.DefaultIndex };
                defaultIdxField.AddToClassList("form-field");
                defaultIdxField.RegisterValueChangedCallback(evt => { group.DefaultIndex = evt.newValue; EditorUtility.SetDirty(group); });
                paramCard.Add(defaultIdxField);

                var savedToggle = new Toggle("记住状态 (Saved)") { value = group.Saved };
                savedToggle.AddToClassList("form-toggle");
                savedToggle.RegisterValueChangedCallback(evt => { group.Saved = evt.newValue; EditorUtility.SetDirty(group); });
                paramCard.Add(savedToggle);

                var syncedToggle = new Toggle("网络同步 (Synced)") { value = group.Synced };
                syncedToggle.AddToClassList("form-toggle");
                syncedToggle.RegisterValueChangedCallback(evt => { group.Synced = evt.newValue; EditorUtility.SetDirty(group); RecalculateBudget(); itemTreeView?.Rebuild(); });
                paramCard.Add(syncedToggle);
            }
            else if (item is NDRadialPuppet radial)
            {
                var pNameField = new TextField("表达式参数 (Parameter):") { value = radial.ParameterName };
                pNameField.AddToClassList("form-field");
                pNameField.RegisterValueChangedCallback(evt => { radial.ParameterName = evt.newValue; EditorUtility.SetDirty(radial); RecalculateBudget(); itemTreeView?.Rebuild(); });
                paramCard.Add(pNameField);

                var defaultValSlider = new Slider("默认浮点值 (0.0 ~ 1.0):", 0f, 1f) { value = radial.DefaultValue };
                defaultValSlider.AddToClassList("form-field");
                defaultValSlider.RegisterValueChangedCallback(evt => { radial.DefaultValue = evt.newValue; EditorUtility.SetDirty(radial); });
                paramCard.Add(defaultValSlider);

                var savedToggle = new Toggle("记住状态 (Saved)") { value = radial.Saved };
                savedToggle.AddToClassList("form-toggle");
                savedToggle.RegisterValueChangedCallback(evt => { radial.Saved = evt.newValue; EditorUtility.SetDirty(radial); });
                paramCard.Add(savedToggle);

                var syncedToggle = new Toggle("网络同步 (Synced)") { value = radial.Synced };
                syncedToggle.AddToClassList("form-toggle");
                syncedToggle.RegisterValueChangedCallback(evt => { radial.Synced = evt.newValue; EditorUtility.SetDirty(radial); RecalculateBudget(); itemTreeView?.Rebuild(); });
                paramCard.Add(syncedToggle);
            }
            else if (item is NDSubMenu subMenu)
            {
                var hintLabel = new Label("此组件为子菜单目录文件夹，可用于在 VRChat 菜单中组织下级子菜单或分类项目。");
                hintLabel.AddToClassList("form-toggle");
                paramCard.Add(hintLabel);
            }

            inspectorContainer.Add(paramCard);

            // =========================================================================
            // 3. Card 2: 控制目标列表 (Controlled Target Objects, BlendShapes & Materials)
            // =========================================================================
            if (item is NDToggleItem toggleItem)
            {
                var targetCard = new VisualElement();
                targetCard.AddToClassList("inspector-card");

                var targetHeader = new VisualElement();
                targetHeader.AddToClassList("card-heading-row");
                var targetTitle = new Label($"受控目标清单 ({toggleItem.objectTargets.Count} 个物体, {toggleItem.blendShapeTargets.Count} 个形态键, {toggleItem.materialTargets.Count} 个材质属性)");
                targetTitle.AddToClassList("card-heading");
                targetHeader.Add(targetTitle);
                targetCard.Add(targetHeader);

                // 1. GameObject Targets PropertyField
                var objProp = serializedObject.FindProperty("objectTargets");
                if (objProp != null)
                {
                    var objField = new PropertyField(objProp, "GameObject 物体显隐控制列表:");
                    objField.Bind(serializedObject);
                    targetCard.Add(objField);
                }

                // 2. BlendShape Targets PropertyField
                var bsProp = serializedObject.FindProperty("blendShapeTargets");
                if (bsProp != null)
                {
                    var bsField = new PropertyField(bsProp, "形态键 (BlendShape) 动画控制列表:");
                    bsField.style.marginTop = 12;
                    bsField.Bind(serializedObject);
                    targetCard.Add(bsField);
                }

                // 3. Material Targets PropertyField
                var matProp = serializedObject.FindProperty("materialTargets");
                if (matProp != null)
                {
                    var matField = new PropertyField(matProp, "材质属性 (Material Property) 动画控制列表:");
                    matField.style.marginTop = 12;
                    matField.Bind(serializedObject);
                    targetCard.Add(matField);
                }

                inspectorContainer.Add(targetCard);
            }
            else if (item is NDRadialPuppet radialItem)
            {
                var targetCard = new VisualElement();
                targetCard.AddToClassList("inspector-card");

                var targetHeader = new VisualElement();
                targetHeader.AddToClassList("card-heading-row");
                var targetTitle = new Label($"受控目标清单 ({radialItem.blendShapeTargets.Count} 个形态键滑条, {radialItem.materialTargets.Count} 个材质滑条)");
                targetTitle.AddToClassList("card-heading");
                targetHeader.Add(targetTitle);
                targetCard.Add(targetHeader);

                var bsProp = serializedObject.FindProperty("blendShapeTargets");
                if (bsProp != null)
                {
                    var bsField = new PropertyField(bsProp, "形态键 (BlendShape) 0~100% 连续滑条控制:");
                    bsField.Bind(serializedObject);
                    targetCard.Add(bsField);
                }

                var matProp = serializedObject.FindProperty("materialTargets");
                if (matProp != null)
                {
                    var matField = new PropertyField(matProp, "材质属性 (Material Property) 0~100% 连续滑条控制:");
                    matField.style.marginTop = 8;
                    matField.Bind(serializedObject);
                    targetCard.Add(matField);
                }

                inspectorContainer.Add(targetCard);
            }

            // =========================================================================
            // 4. Card 3: 场景实时预览与效果测试 (Live Scene Preview)
            // =========================================================================
            if (item is NDToggleItem previewToggle)
            {
                var previewCard = new VisualElement();
                previewCard.AddToClassList("preview-card");

                var pInfo = new VisualElement();
                var pTitle = new Label("场景即时效果测试");
                pTitle.AddToClassList("preview-title");
                var pDesc = new Label("可直接在 Scene 窗口中预览开关穿脱效果，无需进入 Play Mode");
                pDesc.AddToClassList("preview-desc");
                pInfo.Add(pTitle);
                pInfo.Add(pDesc);
                previewCard.Add(pInfo);

                var previewBtn = new Button();
                previewBtn.AddToClassList("btn");

                void UpdatePreviewBtnState()
                {
                    var currentPrimary = previewToggle.objectTargets.Find(x => x.activeWhenOn && x.targetObject != null)?.targetObject;
                    bool isOn = currentPrimary != null && currentPrimary.activeSelf;
                    previewBtn.text = isOn ? "脱下 / 关闭预览" : "换上 / 开启预览";
                    previewBtn.RemoveFromClassList("btn-green");
                    previewBtn.RemoveFromClassList("btn-dark");
                    previewBtn.AddToClassList(isOn ? "btn-dark" : "btn-green");
                }

                previewBtn.clickable.clicked += () =>
                {
                    var currentPrimary = previewToggle.objectTargets.Find(x => x.activeWhenOn && x.targetObject != null)?.targetObject;
                    bool isOn = currentPrimary != null && currentPrimary.activeSelf;
                    bool targetState = !isOn; // If currently ON, turn OFF. If currently OFF, turn ON!

                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Toggle Scene Preview");

                    foreach (var target in previewToggle.objectTargets)
                    {
                        if (target.targetObject != null)
                        {
                            Undo.RecordObject(target.targetObject, "Toggle Active State");
                            target.targetObject.SetActive(targetState ? target.activeWhenOn : !target.activeWhenOn);
                        }
                    }

                    foreach (var bs in previewToggle.blendShapeTargets)
                    {
                        if (bs.skinnedMeshRenderer != null && bs.skinnedMeshRenderer.sharedMesh != null)
                        {
                            int bsIdx = bs.skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(bs.blendShapeName);
                            if (bsIdx >= 0)
                            {
                                Undo.RecordObject(bs.skinnedMeshRenderer, "Toggle BlendShape Weight");
                                bs.skinnedMeshRenderer.SetBlendShapeWeight(bsIdx, targetState ? bs.valueWhenOn : bs.valueWhenOff);
                            }
                        }
                    }

                    foreach (var mat in previewToggle.materialTargets)
                    {
                        if (mat.renderer != null && !string.IsNullOrEmpty(mat.propertyName))
                        {
                            Undo.RecordObject(mat.renderer, "Toggle Material Property");
                            var block = new MaterialPropertyBlock();
                            mat.renderer.GetPropertyBlock(block, mat.materialIndex);
                            block.SetFloat(mat.propertyName, targetState ? mat.valueWhenOn : mat.valueWhenOff);
                            mat.renderer.SetPropertyBlock(block, mat.materialIndex);
                        }
                    }

                    UpdatePreviewBtnState();
                };

                UpdatePreviewBtnState();
                previewCard.Add(previewBtn);
                inspectorContainer.Add(previewCard);
            }
        }

        private string BuildBreadcrumbPath(MonoBehaviour item)
        {
            if (item == null) return "";

            var pathList = new List<string>();
            var curr = item.transform;

            while (curr != null && (currentAvatar == null || curr != currentAvatar.transform))
            {
                var comp = curr.GetComponent<INDMenuItem>();
                if (comp != null && !string.IsNullOrEmpty(comp.MenuName))
                {
                    pathList.Insert(0, comp.MenuName);
                }
                else
                {
                    pathList.Insert(0, curr.name);
                }
                curr = curr.parent;
            }

            return "层级路径: " + string.Join(" > ", pathList);
        }

        private void HideItemInspector()
        {
            if (emptyState != null) emptyState.style.display = DisplayStyle.Flex;
            if (inspectorContainer != null)
            {
                inspectorContainer.style.display = DisplayStyle.None;
                inspectorContainer.Clear();
            }
        }

        private void AddComponentToSelection<T>() where T : MonoBehaviour, INDMenuItem
        {
            var target = Selection.activeGameObject;
            if (target == null)
            {
                if (currentAvatar == null)
                {
                    EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 中选择一个物体或指定 Avatar！", "确定");
                    return;
                }
                target = new GameObject(typeof(T).Name);
                target.transform.SetParent(currentAvatar.transform, false);
                Undo.RegisterCreatedObjectUndo(target, "Create Menu GameObject");
            }

            var comp = target.AddComponent<T>();
            comp.MenuName = target.name;

            if (comp is NDToggleItem toggle)
            {
                toggle.ParameterName = "Toggle_" + target.name.Replace(" ", "_");
                toggle.objectTargets.Add(new GameObjectToggleTarget { targetObject = target, activeWhenOn = true });
            }

            Undo.RegisterCreatedObjectUndo(comp, "Add Menu Component");
            RefreshAll();
            ShowItemInspector(comp);
        }

        private void RecalculateBudget()
        {
            int baseBits = 0;
            int extensionBits = 0;

            if (currentAvatar != null && currentAvatar.expressionParameters != null)
            {
                baseBits = currentAvatar.expressionParameters.CalcTotalCost();
            }

            foreach (var comp in allComponents)
            {
                if (comp is INDMenuItem menuItem)
                {
                    extensionBits += menuItem.GetBitCost();
                }
            }

            int totalBits = baseBits + extensionBits;
            int remaining = 256 - totalBits;

            if (budgetStatusLabel != null)
            {
                budgetStatusLabel.text = $"{totalBits} / 256 bits (基础 {baseBits}b + 扩展 {extensionBits}b) - 剩余 {remaining} bits";
            }

            if (progressFill != null)
            {
                float percent = Mathf.Clamp01(totalBits / 256f) * 100f;
                progressFill.style.width = Length.Percent(percent);

                if (totalBits > 256)
                {
                    progressFill.style.backgroundColor = new StyleColor(new Color(0.9f, 0.2f, 0.2f));
                }
                else if (totalBits > 200)
                {
                    progressFill.style.backgroundColor = new StyleColor(new Color(0.95f, 0.65f, 0.15f));
                }
                else
                {
                    progressFill.style.backgroundColor = new StyleColor(new Color(0.2f, 0.8f, 0.4f));
                }
            }
        }

        private void TogglePlayMode()
        {
            EditorApplication.isPlaying = !EditorApplication.isPlaying;
        }

        private void ManualBakeAssets()
        {
            if (currentAvatar == null)
            {
                EditorUtility.DisplayDialog("提示", "请先指定目标 Avatar！", "确定");
                return;
            }

            string avatarName = currentAvatar.gameObject.name;
            string outputFolder = $"Assets/ND_Generated_{avatarName}";
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                AssetDatabase.CreateFolder("Assets", $"ND_Generated_{avatarName}");
            }

            // 1. Gather all components
            var toggles = currentAvatar.GetComponentsInChildren<NDToggleItem>(true);
            var groups = currentAvatar.GetComponentsInChildren<NDToggleGroup>(true);
            var radials = currentAvatar.GetComponentsInChildren<NDRadialPuppet>(true);
            var subMenus = currentAvatar.GetComponentsInChildren<NDSubMenu>(true);

            // 2. Generate Expression Parameters
            var ep = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            var paramList = new List<VRCExpressionParameters.Parameter>();

            foreach (var t in toggles)
            {
                if (t.Synced && !string.IsNullOrEmpty(t.ParameterName))
                {
                    if (!paramList.Exists(p => p.name == t.ParameterName))
                    {
                        paramList.Add(new VRCExpressionParameters.Parameter
                        {
                            name = t.ParameterName,
                            valueType = t.UseIntParameter ? VRCExpressionParameters.ValueType.Int : VRCExpressionParameters.ValueType.Bool,
                            defaultValue = t.UseIntParameter ? t.ParameterValue : (t.DefaultValue ? 1f : 0f),
                            saved = t.Saved
                        });
                    }
                }
            }

            foreach (var g in groups)
            {
                if (g.Synced && !string.IsNullOrEmpty(g.ParameterName))
                {
                    if (!paramList.Exists(p => p.name == g.ParameterName))
                    {
                        paramList.Add(new VRCExpressionParameters.Parameter
                        {
                            name = g.ParameterName,
                            valueType = VRCExpressionParameters.ValueType.Int,
                            defaultValue = g.DefaultIndex,
                            saved = g.Saved
                        });
                    }
                }
            }

            foreach (var r in radials)
            {
                if (r.Synced && !string.IsNullOrEmpty(r.ParameterName))
                {
                    if (!paramList.Exists(p => p.name == r.ParameterName))
                    {
                        paramList.Add(new VRCExpressionParameters.Parameter
                        {
                            name = r.ParameterName,
                            valueType = VRCExpressionParameters.ValueType.Float,
                            defaultValue = r.DefaultValue,
                            saved = r.Saved
                        });
                    }
                }
            }

            ep.parameters = paramList.ToArray();
            string epPath = $"{outputFolder}/{avatarName}_Parameters.asset";
            AssetDatabase.CreateAsset(ep, epPath);

            // 3. Generate FX Animator Controller
            string fxPath = $"{outputFolder}/{avatarName}_FX.controller";
            var fxCtrl = AnimatorController.CreateAnimatorControllerAtPath(fxPath);

            var processedIntParams = new HashSet<string>();
            foreach (var t in toggles)
            {
                if (t.UseIntParameter)
                {
                    if (processedIntParams.Add(t.ParameterName))
                    {
                        var intToggles = new List<NDToggleItem>();
                        foreach (var other in toggles)
                        {
                            if (other.UseIntParameter && other.ParameterName == t.ParameterName)
                            {
                                intToggles.Add(other);
                            }
                        }

                        AnimationGenerator.CreateSharedIntGroupClips(
                            currentAvatar.transform,
                            t.ParameterName,
                            intToggles,
                            out var allOffClip,
                            out var stateClips
                        );

                        string allOffPath = $"{outputFolder}/{avatarName}_{t.ParameterName}_AllOff.anim";
                        AssetDatabase.CreateAsset(allOffClip, allOffPath);

                        var stateTupleList = new List<(int val, string name, AnimationClip clip)>();
                        foreach (var (val, name, clip) in stateClips)
                        {
                            string clipPath = $"{outputFolder}/{avatarName}_{t.ParameterName}_{val}.anim";
                            AssetDatabase.CreateAsset(clip, clipPath);
                            stateTupleList.Add((val, name, clip));
                        }

                        int defaultVal = intToggles.Count > 0 ? intToggles[0].ParameterValue : 1;
                        bool allowAllOff = t.AllowAllOff;
                        ControllerGenerator.AddSharedIntToggleLayer(fxCtrl, t.ParameterName, defaultVal, allOffClip, stateTupleList, allowAllOff);
                    }
                }
                else
                {
                    AnimationGenerator.CreateToggleClips(currentAvatar.transform, t, out var offClip, out var onClip);
                    string onPath = $"{outputFolder}/{avatarName}_{t.MenuName}_On.anim";
                    string offPath = $"{outputFolder}/{avatarName}_{t.MenuName}_Off.anim";
                    AssetDatabase.CreateAsset(onClip, onPath);
                    AssetDatabase.CreateAsset(offClip, offPath);

                    ControllerGenerator.AddToggleLayer(fxCtrl, t, offClip, onClip);
                }
            }

            foreach (var g in groups)
            {
                var groupClips = AnimationGenerator.CreateToggleGroupClips(currentAvatar.transform, g);
                for (int i = 0; i < groupClips.Count; i++)
                {
                    string cPath = $"{outputFolder}/{avatarName}_{g.MenuName}_Option_{i}.anim";
                    AssetDatabase.CreateAsset(groupClips[i], cPath);
                }
                ControllerGenerator.AddToggleGroupLayer(fxCtrl, g, groupClips);
            }

            foreach (var r in radials)
            {
                AnimationGenerator.CreateRadialPuppetClips(currentAvatar.transform, r, out var minClip, out var maxClip);
                string minPath = $"{outputFolder}/{avatarName}_{r.MenuName}_Min.anim";
                string maxPath = $"{outputFolder}/{avatarName}_{r.MenuName}_Max.anim";
                AssetDatabase.CreateAsset(minClip, minPath);
                AssetDatabase.CreateAsset(maxClip, maxPath);

                ControllerGenerator.AddRadialPuppetLayer(fxCtrl, r, minClip, maxClip);
            }

            // 4. Generate Expressions Menu
            var rootMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            string rootMenuPath = $"{outputFolder}/{avatarName}_RootMenu.asset";
            AssetDatabase.CreateAsset(rootMenu, rootMenuPath);

            var createdSubMenus = new Dictionary<NDSubMenu, VRCExpressionsMenu>();
            foreach (var sm in subMenus)
            {
                var childMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                string childMenuPath = $"{outputFolder}/{avatarName}_SubMenu_{sm.MenuName}.asset";
                AssetDatabase.CreateAsset(childMenu, childMenuPath);
                createdSubMenus[sm] = childMenu;
            }

            foreach (var kvp in createdSubMenus)
            {
                var smComp = kvp.Key;
                var parentSm = smComp.transform.parent != null ? smComp.transform.parent.GetComponentInParent<NDSubMenu>() : null;
                var targetMenu = (parentSm != null && createdSubMenus.ContainsKey(parentSm)) ? createdSubMenus[parentSm] : rootMenu;

                targetMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = smComp.MenuName,
                    icon = smComp.Icon,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = kvp.Value
                });
            }

            foreach (var comp in allComponents)
            {
                if (comp is NDSubMenu) continue;

                var parentSm = comp.transform.parent != null ? comp.transform.parent.GetComponentInParent<NDSubMenu>() : null;
                var targetMenu = (parentSm != null && createdSubMenus.ContainsKey(parentSm)) ? createdSubMenus[parentSm] : rootMenu;

                if (comp is NDToggleItem toggle)
                {
                    targetMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = toggle.MenuName,
                        icon = toggle.Icon,
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        parameter = new VRCExpressionsMenu.Control.Parameter { name = toggle.ParameterName },
                        value = toggle.UseIntParameter ? toggle.ParameterValue : 1f
                    });
                }
                else if (comp is NDToggleGroup group)
                {
                    var groupMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    string gMenuPath = $"{outputFolder}/{avatarName}_GroupMenu_{group.MenuName}.asset";
                    AssetDatabase.CreateAsset(groupMenu, gMenuPath);

                    targetMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = group.MenuName,
                        icon = group.Icon,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = groupMenu
                    });

                    for (int i = 0; i < group.options.Count; i++)
                    {
                        groupMenu.controls.Add(new VRCExpressionsMenu.Control
                        {
                            name = group.options[i].optionName,
                            icon = group.options[i].icon,
                            type = VRCExpressionsMenu.Control.ControlType.Toggle,
                            parameter = new VRCExpressionsMenu.Control.Parameter { name = group.ParameterName },
                            value = i
                        });
                    }
                }
                else if (comp is NDRadialPuppet radial)
                {
                    targetMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = radial.MenuName,
                        icon = radial.Icon,
                        type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                        subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = radial.ParameterName } }
                    });
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("烘焙成功", $"实体资产已成功生成至工程目录：\n{outputFolder}\n\n包含：\n- FX Controller\n- Expressions Menu\n- Expression Parameters\n- 独立 Animation Clips", "确定");
        }

        #endregion
    }
}
