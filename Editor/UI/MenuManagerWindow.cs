using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public class WizardSubToggle
        {
            public string toggleName = "配件开关";
            public List<GameObject> targets = new List<GameObject>();
            public bool defaultValue = true;
        }

        public class WizardItem
        {
            public GameObject gameObject;
            public string displayName;
            public List<GameObject> mainTargets = new List<GameObject>();
            public List<WizardSubToggle> subToggles = new List<WizardSubToggle>();
            public HashSet<GameObject> selectedSubMeshes = new HashSet<GameObject>();
            public bool isExpanded = true;
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
        private VisualElement wzDroppedItemsList;
        private Button wzClearItemsBtn;
        private Toggle wzAutoSubToggles;
        private Toggle wzAutoMutualExclusive;
        private Toggle wzPreventNudityToggle;
        private Toggle wzAutoThumbnailToggle;
        private VisualElement wzOutfitOptionsCard;
        private Label wzEstBudgetBadge;
        private VisualElement wzTreePreviewContainer;
        private Button wzGenerateBtn;

        private Button viewParamInspectorBtn;
        private VisualElement paramInspectorViewContainer;
        private string paramSearchKeyword = "";
        private int paramFilterIndex = 0; // 0: All, 1: Synced, 2: Base, 3: NDMenu, 4: Optimizable

        public enum ViewMode { Wizard, Manager, ParamInspector }

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
            viewParamInspectorBtn = rootVisualElement.Q<Button>("viewParamInspectorBtn");
            wizardViewContainer = rootVisualElement.Q<VisualElement>("wizardViewContainer");
            managerViewContainer = rootVisualElement.Q<TwoPaneSplitView>("managerViewContainer");
            paramInspectorViewContainer = rootVisualElement.Q<VisualElement>("paramInspectorViewContainer");

            playModeBtn = rootVisualElement.Q<Button>("playModeBtn");
            manualBakeBtn = rootVisualElement.Q<Button>("manualBakeBtn");
            var refreshBtn = rootVisualElement.Q<Button>("refreshBtn");

            progressFill = rootVisualElement.Q<VisualElement>("progressFill");
            budgetStatusLabel = rootVisualElement.Q<Label>("budgetStatusLabel");
            var footerBudgetArea = rootVisualElement.Q<VisualElement>("footerBudgetArea");

            // Setup View Switching Tabs
            viewWizardBtn?.RegisterCallback<ClickEvent>(_ => SwitchView(ViewMode.Wizard));
            viewManagerBtn?.RegisterCallback<ClickEvent>(_ => SwitchView(ViewMode.Manager));
            viewParamInspectorBtn?.RegisterCallback<ClickEvent>(_ => SwitchView(ViewMode.ParamInspector));
            footerBudgetArea?.RegisterCallback<ClickEvent>(_ => SwitchView(ViewMode.ParamInspector));

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

        private void SwitchView(ViewMode mode)
        {
            if (wizardViewContainer == null || managerViewContainer == null || paramInspectorViewContainer == null) return;

            wizardViewContainer.style.display = (mode == ViewMode.Wizard) ? DisplayStyle.Flex : DisplayStyle.None;
            managerViewContainer.style.display = (mode == ViewMode.Manager) ? DisplayStyle.Flex : DisplayStyle.None;
            paramInspectorViewContainer.style.display = (mode == ViewMode.ParamInspector) ? DisplayStyle.Flex : DisplayStyle.None;

            viewWizardBtn?.EnableInClassList("view-switch-active", mode == ViewMode.Wizard);
            viewManagerBtn?.EnableInClassList("view-switch-active", mode == ViewMode.Manager);
            viewParamInspectorBtn?.EnableInClassList("view-switch-active", mode == ViewMode.ParamInspector);

            if (mode == ViewMode.Manager) ExpandAllTreeItems();
            if (mode == ViewMode.ParamInspector) RenderParamInspectorView();
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
            wzDroppedItemsList = rootVisualElement.Q<VisualElement>("wzDroppedItemsList");
            wzClearItemsBtn = rootVisualElement.Q<Button>("wzClearItemsBtn");

            wzAutoSubToggles = rootVisualElement.Q<Toggle>("wzAutoSubToggles");
            wzAutoMutualExclusive = rootVisualElement.Q<Toggle>("wzAutoMutualExclusive");
            wzPreventNudityToggle = rootVisualElement.Q<Toggle>("wzPreventNudityToggle");
            wzAutoThumbnailToggle = rootVisualElement.Q<Toggle>("wzAutoThumbnailToggle");
            wzOutfitOptionsCard = rootVisualElement.Q<VisualElement>("wzOutfitOptionsCard");

            wzEstBudgetBadge = rootVisualElement.Q<Label>("wzEstBudgetBadge");
            wzTreePreviewContainer = rootVisualElement.Q<VisualElement>("wzTreePreviewContainer");
            wzGenerateBtn = rootVisualElement.Q<Button>("wzGenerateBtn");

            // Setup Mode Switch Tabs
            wzTabExclusive?.RegisterCallback<ClickEvent>(_ => SetWizardMode(WizardMode.Exclusive));
            wzTabProps?.RegisterCallback<ClickEvent>(_ => SetWizardMode(WizardMode.Props));

            // Setup Presets
            wzPresetWardrobe?.RegisterCallback<ClickEvent>(_ => { if (wzFolderNameField != null) wzFolderNameField.value = "衣柜"; SetWizardMode(WizardMode.Exclusive); UpdateProspectiveTreePreview(); });
            wzPresetHair?.RegisterCallback<ClickEvent>(_ => { if (wzFolderNameField != null) wzFolderNameField.value = "发型"; SetWizardMode(WizardMode.Exclusive); UpdateProspectiveTreePreview(); });
            wzPresetBody?.RegisterCallback<ClickEvent>(_ => { if (wzFolderNameField != null) wzFolderNameField.value = "体型"; SetWizardMode(WizardMode.Exclusive); UpdateProspectiveTreePreview(); });
            wzPresetProps?.RegisterCallback<ClickEvent>(_ => { if (wzFolderNameField != null) wzFolderNameField.value = "饰品"; SetWizardMode(WizardMode.Props); UpdateProspectiveTreePreview(); });

            wzFolderNameField?.RegisterValueChangedCallback(_ => UpdateProspectiveTreePreview());
            wzAutoMutualExclusive?.RegisterValueChangedCallback(_ => UpdateProspectiveTreePreview());

            wzClearItemsBtn?.RegisterCallback<ClickEvent>(_ =>
            {
                wizardItems.Clear();
                UpdateWizardDroppedList();
            });

            var wzGenAllSubTogglesGlobalBtn = rootVisualElement.Q<Button>("wzGenAllSubTogglesGlobalBtn");
            wzGenAllSubTogglesGlobalBtn?.RegisterCallback<ClickEvent>(_ =>
            {
                foreach (var item in wizardItems)
                {
                    var allMeshes = GetMeshGameObjects(item.gameObject);
                    var subMeshes = allMeshes.Where(m => m != item.gameObject).ToList();
                    var targetList = subMeshes.Count > 0 ? subMeshes : allMeshes;
                    foreach (var meshGo in targetList)
                    {
                        if (!item.subToggles.Exists(st => st.targets.Contains(meshGo)))
                        {
                            var newSt = new WizardSubToggle
                            {
                                toggleName = meshGo.name,
                                defaultValue = meshGo.activeSelf
                            };
                            newSt.targets.Add(meshGo);
                            item.subToggles.Add(newSt);
                        }
                    }
                }
                UpdateWizardDroppedList();
            });

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

            UpdateProspectiveTreePreview();
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

                var droppedGos = new List<GameObject>();
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
                                if (avatarField != null) avatarField.SetValueWithoutNotify(desc.gameObject);
                            }
                        }
                        if (!droppedGos.Contains(go)) droppedGos.Add(go);
                    }
                }

                // If user dragged a single top-level category container (e.g. "头发" or "衣服" folder with multiple children)
                if (droppedGos.Count == 1)
                {
                    var singleGo = droppedGos[0];
                    if (!IsValidSubMeshItem(singleGo.transform) && singleGo.transform.childCount > 1)
                    {
                        droppedGos.Clear();
                        foreach (Transform child in singleGo.transform)
                        {
                            droppedGos.Add(child.gameObject);
                        }
                    }
                }

                // Filter out any child GameObjects whose parent/ancestor is also in the dragged list or already in wizardItems
                var filteredGos = new List<GameObject>();
                foreach (var go in droppedGos)
                {
                    bool hasParentInDrag = droppedGos.Exists(other => other != go && go.transform.IsChildOf(other.transform));
                    bool hasParentInWizard = wizardItems.Exists(w => w.gameObject != null && w.gameObject != go && go.transform.IsChildOf(w.gameObject.transform));

                    if (!hasParentInDrag && !hasParentInWizard)
                    {
                        filteredGos.Add(go);
                    }
                }

                foreach (var go in filteredGos)
                {
                    AddWizardItem(go);
                }

                UpdateWizardDroppedList();
            });
        }

        private void AddWizardItem(GameObject go)
        {
            if (go == null || wizardItems.Exists(x => x.gameObject == go)) return;

            var item = new WizardItem
            {
                gameObject = go,
                displayName = go.name,
                isExpanded = true
            };

            var meshObjects = GetMeshGameObjects(go);

            // Always add all mesh objects to mainTargets by default so the main switch controls all components
            foreach (var obj in meshObjects)
            {
                if (!item.mainTargets.Contains(obj)) item.mainTargets.Add(obj);
            }
            if (!item.mainTargets.Contains(go))
            {
                item.mainTargets.Add(go);
            }

            wizardItems.Add(item);
        }

        private List<GameObject> GetMeshGameObjects(GameObject root)
        {
            var list = new List<GameObject>();
            if (root == null) return list;

            var allTrans = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTrans)
            {
                if (IsValidSubMeshItem(t))
                {
                    if (!list.Contains(t.gameObject)) list.Add(t.gameObject);
                }
            }
            if (list.Count == 0) list.Add(root);
            return list;
        }

        private string GuessCommonClusterName(List<GameObject> objs)
        {
            if (objs == null || objs.Count == 0) return "配件开关";
            if (objs.Count == 1) return objs[0].name;

            // Find longest common prefix (case-insensitive)
            string prefix = objs[0].name;
            for (int i = 1; i < objs.Count; i++)
            {
                string name = objs[i].name;
                int len = 0;
                while (len < prefix.Length && len < name.Length && char.ToLower(prefix[len]) == char.ToLower(name[len]))
                {
                    len++;
                }
                prefix = prefix.Substring(0, len);
            }

            prefix = prefix.TrimEnd('_', '-', ' ', '.');
            if (string.IsNullOrEmpty(prefix) || prefix.Length < 2)
            {
                return System.Text.RegularExpressions.Regex.Replace(objs[0].name, @"[_\-\s]*(L|R|left|right|\d+)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            }
            return prefix;
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

                var card = new VisualElement();
                card.AddToClassList("wz-item-card");

                // Header
                var header = new VisualElement();
                header.AddToClassList("wz-item-card-header");

                var foldBtn = new Button(() =>
                {
                    item.isExpanded = !item.isExpanded;
                    UpdateWizardDroppedList();
                })
                {
                    text = item.isExpanded ? "▼" : "▶"
                };
                foldBtn.AddToClassList("wz-foldout-btn");

                var badge = new Label(index == 0 ? "#1 (默认)" : $"#{index + 1}");
                badge.AddToClassList("item-index-badge");
                if (index == 0) badge.AddToClassList("item-index-default");

                // Order Buttons
                var upBtn = new Button(() =>
                {
                    if (index > 0)
                    {
                        var temp = wizardItems[index];
                        wizardItems[index] = wizardItems[index - 1];
                        wizardItems[index - 1] = temp;
                        UpdateWizardDroppedList();
                    }
                }) { text = "▲" };
                upBtn.AddToClassList("item-order-btn");
                if (index == 0) upBtn.SetEnabled(false);

                var downBtn = new Button(() =>
                {
                    if (index < wizardItems.Count - 1)
                    {
                        var temp = wizardItems[index];
                        wizardItems[index] = wizardItems[index + 1];
                        wizardItems[index + 1] = temp;
                        UpdateWizardDroppedList();
                    }
                }) { text = "▼" };
                downBtn.AddToClassList("item-order-btn");
                if (index == wizardItems.Count - 1) downBtn.SetEnabled(false);

                // Name Field
                var nameInput = new TextField();
                nameInput.value = item.displayName;
                nameInput.AddToClassList("wz-item-name-field");
                nameInput.RegisterValueChangedCallback(evt =>
                {
                    item.displayName = string.IsNullOrEmpty(evt.newValue) ? item.gameObject.name : evt.newValue;
                    UpdateProspectiveTreePreview();
                });

                // Summary Badge (Count of main parts & sub toggles)
                int mainTargetCount = item.mainTargets.Count > 0 ? item.mainTargets.Count : 1;
                var summaryBadge = new Label($"主控 {mainTargetCount} | 子控 {item.subToggles.Count}");
                summaryBadge.AddToClassList("wz-summary-badge");

                // Add Sub-toggle button directly on header
                var addSubBtn = new Button(() =>
                {
                    item.subToggles.Add(new WizardSubToggle
                    {
                        toggleName = $"配件 {item.subToggles.Count + 1}",
                        defaultValue = true
                    });
                    item.isExpanded = true;
                    UpdateWizardDroppedList();
                })
                {
                    text = "+ 子开关"
                };
                addSubBtn.AddToClassList("btn-add-subtoggle");
                addSubBtn.style.marginRight = 4;

                var genAllSubHeaderBtn = new Button(() =>
                {
                    var allMeshes = GetMeshGameObjects(item.gameObject);
                    var subMeshes = allMeshes.Where(m => m != item.gameObject).ToList();
                    var targetList = subMeshes.Count > 0 ? subMeshes : allMeshes;
                    foreach (var meshGo in targetList)
                    {
                        if (!item.subToggles.Exists(st => st.targets.Contains(meshGo)))
                        {
                            var newSt = new WizardSubToggle
                            {
                                toggleName = meshGo.name,
                                defaultValue = meshGo.activeSelf
                            };
                            newSt.targets.Add(meshGo);
                            item.subToggles.Add(newSt);
                        }
                    }
                    item.isExpanded = true;
                    UpdateWizardDroppedList();
                })
                {
                    text = "全建子开关",
                    tooltip = "一键为该物品下的所有内部散件各建立一个独立子开关"
                };
                genAllSubHeaderBtn.AddToClassList("btn-gen-all-subtoggles");
                genAllSubHeaderBtn.style.marginRight = 4;

                // Delete Button
                var delBtn = new Button(() =>
                {
                    wizardItems.RemoveAt(index);
                    UpdateWizardDroppedList();
                }) { text = "x" };
                delBtn.AddToClassList("item-del-btn");

                header.Add(foldBtn);
                header.Add(badge);
                header.Add(upBtn);
                header.Add(downBtn);
                header.Add(nameInput);
                header.Add(summaryBadge);
                header.Add(addSubBtn);
                header.Add(genAllSubHeaderBtn);
                header.Add(delBtn);

                card.Add(header);

                // Expanded Body: Detailed Components Assignment
                if (item.isExpanded)
                {
                    var body = new VisualElement();
                    body.AddToClassList("wz-item-body");

                    var allMeshes = GetMeshGameObjects(item.gameObject);
                    var subMeshes = allMeshes.Where(m => m != item.gameObject).ToList();
                    var availableSubTargets = subMeshes.Count > 0 ? subMeshes : allMeshes;

                    // 1. Main Switch Targets Section
                    var mainTitle = new Label("【换装主开关受控部件】（随套装/发型整体显隐）：");
                    mainTitle.AddToClassList("wz-subpart-group-title");
                    body.Add(mainTitle);

                    var mainChipsWrap = new VisualElement();
                    mainChipsWrap.AddToClassList("wz-chips-wrap");

                    if (item.mainTargets.Count == 0)
                    {
                        var tip = new Label("(暂未分配主部件，点击下方可用散件添加)");
                        tip.style.fontSize = 11;
                        tip.style.color = new Color(0.5f, 0.5f, 0.6f);
                        mainChipsWrap.Add(tip);
                    }
                    else
                    {
                        foreach (var targetGo in item.mainTargets.ToArray())
                        {
                            if (targetGo == null) continue;
                            var chip = CreatePartChip(targetGo, "part-chip-main", () =>
                            {
                                item.mainTargets.Remove(targetGo);
                                UpdateWizardDroppedList();
                            });
                            mainChipsWrap.Add(chip);
                        }
                    }
                    body.Add(mainChipsWrap);

                    // 2. Custom Sub Toggles Section
                    if (item.subToggles.Count > 0)
                    {
                        var subTitleRow = new VisualElement();
                        subTitleRow.style.flexDirection = FlexDirection.Row;
                        subTitleRow.style.alignItems = Align.Center;
                        subTitleRow.style.justifyContent = Justify.SpaceBetween;
                        subTitleRow.style.marginBottom = 6;
                        subTitleRow.style.marginTop = 4;

                        var subTitle = new Label("【独立子开关】（支持一个开关同时控制多个散件，如背包、外套）：");
                        subTitle.AddToClassList("wz-subpart-group-title");
                        subTitle.style.marginBottom = 0;
                        subTitle.style.marginTop = 0;

                        var clearSubBtn = new Button(() =>
                        {
                            item.subToggles.Clear();
                            UpdateWizardDroppedList();
                        })
                        {
                            text = "清空子开关"
                        };
                        clearSubBtn.AddToClassList("preset-btn");
                        clearSubBtn.style.paddingLeft = 6;
                        clearSubBtn.style.paddingRight = 6;

                        subTitleRow.Add(subTitle);
                        subTitleRow.Add(clearSubBtn);
                        body.Add(subTitleRow);

                        for (int s = 0; s < item.subToggles.Count; s++)
                        {
                            int subIndex = s;
                            var subToggle = item.subToggles[s];

                            var subBox = new VisualElement();
                            subBox.AddToClassList("wz-subtoggle-box");

                            var subHeader = new VisualElement();
                            subHeader.AddToClassList("wz-subtoggle-header");

                            var subNameInput = new TextField();
                            subNameInput.value = subToggle.toggleName;
                            subNameInput.AddToClassList("wz-subtoggle-name");
                            subNameInput.RegisterValueChangedCallback(evt =>
                            {
                                subToggle.toggleName = evt.newValue;
                                UpdateProspectiveTreePreview();
                            });

                            var subDefaultToggle = new Toggle("默认开") { value = subToggle.defaultValue };
                            subDefaultToggle.AddToClassList("wz-subtoggle-default");
                            subDefaultToggle.RegisterValueChangedCallback(evt => subToggle.defaultValue = evt.newValue);

                            var addTargetToSubBtn = new Button(() =>
                            {
                                var menu = new GenericMenu();
                                foreach (var meshGo in availableSubTargets)
                                {
                                    var targetMesh = meshGo;
                                    bool isAlreadyIn = subToggle.targets.Contains(targetMesh);
                                    menu.AddItem(new GUIContent($"{targetMesh.name}"), isAlreadyIn, () =>
                                    {
                                        if (isAlreadyIn)
                                            subToggle.targets.Remove(targetMesh);
                                        else
                                            subToggle.targets.Add(targetMesh);
                                        UpdateWizardDroppedList();
                                    });
                                }
                                menu.ShowAsContext();
                            })
                            {
                                text = "+ 关联散件"
                            };
                            addTargetToSubBtn.AddToClassList("preset-btn");
                            addTargetToSubBtn.style.marginRight = 6;

                            var delSubBtn = new Button(() =>
                            {
                                item.subToggles.RemoveAt(subIndex);
                                UpdateWizardDroppedList();
                            }) { text = "x" };
                            delSubBtn.AddToClassList("item-del-btn");

                            subHeader.Add(subNameInput);
                            subHeader.Add(subDefaultToggle);
                            subHeader.Add(addTargetToSubBtn);
                            subHeader.Add(delSubBtn);
                            subBox.Add(subHeader);

                            // Sub toggle targets chips
                            var subChipsWrap = new VisualElement();
                            subChipsWrap.AddToClassList("wz-chips-wrap");

                            if (subToggle.targets.Count == 0)
                            {
                                var tip = new Label("(点击上方 [+ 关联散件] 勾选该开关要控制的一个或多个物体)");
                                tip.style.fontSize = 11;
                                tip.style.color = new Color(0.5f, 0.5f, 0.6f);
                                subChipsWrap.Add(tip);
                            }
                            else
                            {
                                foreach (var targetGo in subToggle.targets.ToArray())
                                {
                                    if (targetGo == null) continue;
                                    var chip = CreatePartChip(targetGo, "part-chip-sub", () =>
                                    {
                                        subToggle.targets.Remove(targetGo);
                                        UpdateWizardDroppedList();
                                    });
                                    subChipsWrap.Add(chip);
                                }
                            }
                            subBox.Add(subChipsWrap);

                            body.Add(subBox);
                        }
                    }

                    // 3. Quick Sub-Toggle Creation & Multi-Select Grouping
                    if (subMeshes.Count > 0)
                    {
                        var quickSubHeaderRow = new VisualElement();
                        quickSubHeaderRow.style.flexDirection = FlexDirection.Row;
                        quickSubHeaderRow.style.alignItems = Align.Center;
                        quickSubHeaderRow.style.justifyContent = Justify.SpaceBetween;
                        quickSubHeaderRow.style.marginBottom = 6;
                        quickSubHeaderRow.style.marginTop = 6;

                        var quickSubTitle = new Label($"【所有内部子部件】（共 {subMeshes.Count} 件，勾选可一键合并为单开关）：");
                        quickSubTitle.AddToClassList("wz-subpart-group-title");
                        quickSubTitle.style.marginBottom = 0;
                        quickSubTitle.style.marginTop = 0;

                        var actionsWrap = new VisualElement();
                        actionsWrap.style.flexDirection = FlexDirection.Row;
                        actionsWrap.style.alignItems = Align.Center;

                        int selectedCount = item.selectedSubMeshes.Count;
                        if (selectedCount > 0)
                        {
                            var createGroupBtn = new Button(() =>
                            {
                                var selectedList = new List<GameObject>(item.selectedSubMeshes);
                                string guessedName = GuessCommonClusterName(selectedList);

                                var newSt = new WizardSubToggle
                                {
                                    toggleName = guessedName,
                                    defaultValue = selectedList.Any(g => g != null && g.activeSelf)
                                };
                                newSt.targets.AddRange(selectedList);
                                item.subToggles.Add(newSt);
                                item.selectedSubMeshes.Clear();
                                UpdateWizardDroppedList();
                            })
                            {
                                text = $"合并选中的 {selectedCount} 个部件为新开关",
                                tooltip = $"将当前勾选的 {selectedCount} 个散件合并在一个新子开关中统一控制"
                            };
                            createGroupBtn.AddToClassList("btn-gen-all-subtoggles");
                            createGroupBtn.style.marginRight = 6;
                            actionsWrap.Add(createGroupBtn);

                            var deselectBtn = new Button(() =>
                            {
                                item.selectedSubMeshes.Clear();
                                UpdateWizardDroppedList();
                            })
                            {
                                text = "取消勾选"
                            };
                            deselectBtn.AddToClassList("preset-btn");
                            actionsWrap.Add(deselectBtn);
                        }
                        else
                        {
                            var selectAllBtn = new Button(() =>
                            {
                                foreach (var m in subMeshes)
                                {
                                    item.selectedSubMeshes.Add(m);
                                }
                                UpdateWizardDroppedList();
                            })
                            {
                                text = "全选散件"
                            };
                            selectAllBtn.AddToClassList("preset-btn");
                            actionsWrap.Add(selectAllBtn);
                        }

                        quickSubHeaderRow.Add(quickSubTitle);
                        quickSubHeaderRow.Add(actionsWrap);
                        body.Add(quickSubHeaderRow);

                        var quickSubWrap = new VisualElement();
                        quickSubWrap.AddToClassList("wz-chips-wrap");

                        foreach (var meshGo in subMeshes)
                        {
                            var targetMesh = meshGo;
                            bool isChecked = item.selectedSubMeshes.Contains(targetMesh);

                            var partBox = new VisualElement();
                            partBox.style.flexDirection = FlexDirection.Row;
                            partBox.style.alignItems = Align.Center;
                            partBox.AddToClassList("preset-btn");
                            partBox.style.paddingLeft = 6;
                            partBox.style.paddingRight = 6;
                            partBox.style.marginBottom = 4;

                            if (isChecked)
                            {
                                partBox.style.backgroundColor = new Color(0.18f, 0.28f, 0.55f);
                                partBox.style.borderTopColor = new Color(0.38f, 0.58f, 0.95f);
                                partBox.style.borderBottomColor = new Color(0.38f, 0.58f, 0.95f);
                                partBox.style.borderLeftColor = new Color(0.38f, 0.58f, 0.95f);
                                partBox.style.borderRightColor = new Color(0.38f, 0.58f, 0.95f);
                            }

                            // Checkbox toggle
                            var checkToggle = new Toggle() { value = isChecked };
                            checkToggle.style.marginRight = 4;
                            checkToggle.style.marginBottom = 0;
                            checkToggle.RegisterValueChangedCallback(evt =>
                            {
                                if (evt.newValue)
                                    item.selectedSubMeshes.Add(targetMesh);
                                else
                                    item.selectedSubMeshes.Remove(targetMesh);
                                UpdateWizardDroppedList();
                            });

                            var pingLabel = new Label(targetMesh.name);
                            pingLabel.style.fontSize = 11.5f;
                            pingLabel.style.color = isChecked ? Color.white : new Color(0.9f, 0.92f, 0.98f);
                            pingLabel.style.marginRight = 6;
                            pingLabel.tooltip = "单击在 Hierarchy 中高亮定位，双击查看 3D 预览";
                            pingLabel.RegisterCallback<ClickEvent>(evt =>
                            {
                                EditorGUIUtility.PingObject(targetMesh);
                                Selection.activeGameObject = targetMesh;
                                if (evt.clickCount >= 2)
                                {
                                    NDItemPreviewWindow.ShowPreview(targetMesh);
                                }
                            });

                            var prevBtn = new Label("预览");
                            prevBtn.AddToClassList("part-chip-preview");
                            prevBtn.tooltip = "打开独立窗口查看 3D 渲染与网格数据";
                            prevBtn.RegisterCallback<ClickEvent>(_ => NDItemPreviewWindow.ShowPreview(targetMesh));

                            var addBtn = new Label("+ 单独建开关");
                            addBtn.AddToClassList("part-chip-add");
                            addBtn.tooltip = "以此单件建立独立子开关";
                            addBtn.RegisterCallback<ClickEvent>(_ =>
                            {
                                var newSt = new WizardSubToggle
                                {
                                    toggleName = targetMesh.name,
                                    defaultValue = targetMesh.activeSelf
                                };
                                newSt.targets.Add(targetMesh);
                                item.subToggles.Add(newSt);
                                UpdateWizardDroppedList();
                            });

                            partBox.Add(checkToggle);
                            partBox.Add(pingLabel);
                            partBox.Add(prevBtn);
                            partBox.Add(addBtn);
                            quickSubWrap.Add(partBox);
                        }
                        body.Add(quickSubWrap);
                    }

                    card.Add(body);
                }

                wzDroppedItemsList.Add(card);
            }

            UpdateProspectiveTreePreview();
        }

        private VisualElement CreatePartChip(GameObject targetGo, string chipClass, Action onRemove)
        {
            var chip = new VisualElement();
            chip.AddToClassList("part-chip");
            chip.AddToClassList(chipClass);

            string name = targetGo != null ? targetGo.name : "未知物体";
            var label = new Label(name);
            label.AddToClassList("part-chip-text");
            label.tooltip = "单击在 Hierarchy 中高亮定位，双击打开 3D 预览窗口";
            label.RegisterCallback<ClickEvent>(evt =>
            {
                if (targetGo != null)
                {
                    EditorGUIUtility.PingObject(targetGo);
                    Selection.activeGameObject = targetGo;
                    if (evt.clickCount >= 2)
                    {
                        NDItemPreviewWindow.ShowPreview(targetGo);
                    }
                }
            });

            var prevBtn = new Label("预览");
            prevBtn.AddToClassList("part-chip-preview");
            prevBtn.tooltip = "打开独立窗口查看 3D 模型与网格信息";
            prevBtn.RegisterCallback<ClickEvent>(_ =>
            {
                if (targetGo != null)
                {
                    NDItemPreviewWindow.ShowPreview(targetGo);
                }
            });

            var delBtn = new Label("x");
            delBtn.AddToClassList("part-chip-del");
            delBtn.tooltip = "从本开关受控列表中移除";
            delBtn.RegisterCallback<ClickEvent>(_ => onRemove?.Invoke());

            chip.Add(label);
            chip.Add(prevBtn);
            chip.Add(delBtn);
            return chip;
        }

        private void UpdateProspectiveTreePreview()
        {
            if (wzTreePreviewContainer == null) return;
            wzTreePreviewContainer.Clear();

            if (wizardItems.Count == 0)
            {
                var emptyLabel = new Label("尚未添加任何物品。\n从上方拖入衣服或发型物体后，此处将实时渲染最终生成的菜单目录树。");
                emptyLabel.AddToClassList("placeholder-sub");
                emptyLabel.style.paddingTop = 40;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                wzTreePreviewContainer.Add(emptyLabel);

                if (wzEstBudgetBadge != null)
                {
                    wzEstBudgetBadge.text = "0 / 256 bits";
                    wzEstBudgetBadge.style.color = new Color(0.5f, 0.6f, 0.7f);
                }
                return;
            }

            string rootFolderName = string.IsNullOrEmpty(wzFolderNameField?.value) ? "菜单" : wzFolderNameField.value;
            bool isExclusive = currentWizardMode == WizardMode.Exclusive;
            int estBits = 0;

            // 1. Root Folder Row
            var rootRow = CreateProspectiveRow(0, "d_Folder Icon", $"{rootFolderName} (根菜单)", "badge-folder", "主目录");
            wzTreePreviewContainer.Add(rootRow);

            if (isExclusive)
            {
                estBits += 8; // Shared Int param consumes 8 bits
            }

            // 2. Items
            for (int i = 0; i < wizardItems.Count; i++)
            {
                var item = wizardItems[i];
                if (item == null || item.gameObject == null) continue;
                string itemDisplayName = string.IsNullOrEmpty(item.displayName) ? item.gameObject.name : item.displayName;

                if (isExclusive)
                {
                    // Subfolder for this Outfit/Hair
                    var itemFolderRow = CreateProspectiveRow(1, "d_Folder Icon", itemDisplayName, "badge-folder", "子文件夹");
                    wzTreePreviewContainer.Add(itemFolderRow);

                    // Main Toggle Switch inside subfolder
                    string switchPrefix = (rootFolderName.Contains("发") || rootFolderName.Contains("头")) ? "切换至" : "穿上";
                    int mainCount = item.mainTargets.Count > 0 ? item.mainTargets.Count : 1;
                    var mainToggleRow = CreateProspectiveRow(2, "d_FilterSelectedOnly", $"{switchPrefix} {itemDisplayName}", "badge-slot", $"槽位 #{i + 1} | 控制 {mainCount} 个组件");
                    wzTreePreviewContainer.Add(mainToggleRow);

                    // Sub Toggles inside subfolder
                    foreach (var sub in item.subToggles)
                    {
                        if (sub == null || string.IsNullOrEmpty(sub.toggleName)) continue;
                        int subCount = sub.targets.Count;
                        var subRow = CreateProspectiveRow(2, "d_Toggle Icon", sub.toggleName, "badge-toggle", $"控制 {subCount} 个组件 | 1b");
                        wzTreePreviewContainer.Add(subRow);
                        estBits += 1;
                    }
                }
                else
                {
                    // Props Mode (Direct toggle under root or sub toggles)
                    int count = item.mainTargets.Count + item.subToggles.Sum(s => s.targets.Count);
                    var propRow = CreateProspectiveRow(1, "d_Toggle Icon", itemDisplayName, "badge-toggle", $"控制 {count} 个组件 | 1b");
                    wzTreePreviewContainer.Add(propRow);
                    estBits += 1;
                }
            }

            // Update Budget Badge
            if (wzEstBudgetBadge != null)
            {
                wzEstBudgetBadge.text = $"预计 {estBits} / 256 bits";
                wzEstBudgetBadge.style.color = estBits > 256 ? new Color(0.95f, 0.3f, 0.3f) : new Color(0.3f, 0.85f, 0.5f);
            }
        }

        private VisualElement CreateProspectiveRow(int indentLevel, string iconName, string labelText, string badgeClass, string badgeText)
        {
            var row = new VisualElement();
            row.AddToClassList("prospective-node-row");
            row.style.paddingLeft = indentLevel * 18 + 6;

            var icon = new Image();
            icon.AddToClassList("prospective-icon");
            icon.image = EditorGUIUtility.IconContent(iconName).image;
            row.Add(icon);

            var label = new Label(labelText);
            label.AddToClassList("prospective-label");
            row.Add(label);

            if (!string.IsNullOrEmpty(badgeText))
            {
                var badge = new Label(badgeText);
                badge.AddToClassList("prospective-badge");
                badge.AddToClassList(badgeClass);
                row.Add(badge);
            }

            return row;
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
            SwitchView(ViewMode.Manager);

            EditorUtility.DisplayDialog("生成成功", $"已成功生成【{folderName}】完整菜单体系与多部件控制项！\n已自动为您切换至【菜单层级管理】面板。", "确定");
        }

        private void GenerateExclusiveStructure(string rootFolderName)
        {
            bool mutualExclusive = wzAutoMutualExclusive == null || wzAutoMutualExclusive.value;
            bool preventNudity = wzPreventNudityToggle == null || wzPreventNudityToggle.value;
            bool genThumbnails = wzAutoThumbnailToggle == null || wzAutoThumbnailToggle.value;
            string avatarName = currentAvatar.gameObject.name;

            // 1. Create main root folder
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
                if (item == null || item.gameObject == null) continue;
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

                // Add all main targets (ON)
                var primaryMainList = item.mainTargets.Count > 0 ? item.mainTargets : new List<GameObject> { targetGo };
                foreach (var mTarget in primaryMainList)
                {
                    if (mTarget == null) continue;
                    mainToggle.objectTargets.Add(new GameObjectToggleTarget
                    {
                        targetObject = mTarget,
                        activeWhenOn = true
                    });
                }
                if (!primaryMainList.Contains(targetGo))
                {
                    mainToggle.objectTargets.Add(new GameObjectToggleTarget
                    {
                        targetObject = targetGo,
                        activeWhenOn = true
                    });
                }

                // Turn OFF other items' targets
                if (mutualExclusive)
                {
                    for (int j = 0; j < wizardItems.Count; j++)
                    {
                        if (i == j) continue;
                        var otherItem = wizardItems[j];
                        if (otherItem == null || otherItem.gameObject == null) continue;

                        var otherTargets = otherItem.mainTargets.Count > 0 ? otherItem.mainTargets : new List<GameObject> { otherItem.gameObject };
                        foreach (var otherGo in otherTargets)
                        {
                            if (otherGo != null)
                            {
                                mainToggle.objectTargets.Add(new GameObjectToggleTarget
                                {
                                    targetObject = otherGo,
                                    activeWhenOn = false
                                });
                            }
                        }
                        if (!otherTargets.Contains(otherItem.gameObject))
                        {
                            mainToggle.objectTargets.Add(new GameObjectToggleTarget
                            {
                                targetObject = otherItem.gameObject,
                                activeWhenOn = false
                            });
                        }
                    }
                }

                // 3. Create Custom Multi-Component Sub Toggles
                foreach (var subToggle in item.subToggles)
                {
                    if (subToggle == null || string.IsNullOrEmpty(subToggle.toggleName) || subToggle.targets.Count == 0) continue;

                    Texture2D subIcon = null;
                    var primaryTarget = subToggle.targets[0];
                    if (genThumbnails && primaryTarget != null)
                    {
                        var raw = ThumbnailGenerator.CaptureGameObjectThumbnail(primaryTarget);
                        subIcon = ThumbnailGenerator.SaveThumbnailAsset(raw, avatarName, $"{itemDisplayName}_{subToggle.toggleName}");
                    }

                    var subToggleGo = new GameObject(subToggle.toggleName);
                    subToggleGo.transform.SetParent(itemSubGo.transform, false);
                    var nToggle = subToggleGo.AddComponent<NDToggleItem>();
                    nToggle.MenuName = subToggle.toggleName;
                    nToggle.Icon = subIcon;
                    nToggle.ParameterName = $"Toggle_{itemDisplayName}_{subToggle.toggleName}".Replace(" ", "_");
                    nToggle.DefaultValue = subToggle.defaultValue;

                    // Bind ALL GameObjects in subToggle.targets!
                    foreach (var targetObj in subToggle.targets)
                    {
                        if (targetObj == null) continue;
                        nToggle.objectTargets.Add(new GameObjectToggleTarget
                        {
                            targetObject = targetObj,
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
                if (item == null || item.gameObject == null) continue;
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

                var allTargets = new List<GameObject>(item.mainTargets);
                if (!allTargets.Contains(prop)) allTargets.Add(prop);
                foreach (var st in item.subToggles)
                {
                    foreach (var t in st.targets)
                    {
                        if (!allTargets.Contains(t)) allTargets.Add(t);
                    }
                }

                foreach (var tObj in allTargets)
                {
                    if (tObj == null) continue;
                    toggle.objectTargets.Add(new GameObjectToggleTarget
                    {
                        targetObject = tObj,
                        activeWhenOn = true
                    });
                }
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
            var result = BitBudgetCalculator.Calculate(currentAvatar);

            if (budgetStatusLabel != null)
            {
                budgetStatusLabel.text = result.statusText;
            }

            if (progressFill != null)
            {
                progressFill.style.width = Length.Percent(result.percentage * 100f);
                progressFill.style.backgroundColor = result.barColor;
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

        #region Parameter Inspector View

        private void RenderParamInspectorView()
        {
            if (paramInspectorViewContainer == null) return;
            paramInspectorViewContainer.Clear();

            var result = BitBudgetCalculator.Calculate(currentAvatar);

            // 1. Dashboard Card
            var dashboardCard = new VisualElement();
            dashboardCard.style.backgroundColor = new Color(0.12f, 0.13f, 0.20f);
            dashboardCard.style.borderTopLeftRadius = 8;
            dashboardCard.style.borderTopRightRadius = 8;
            dashboardCard.style.borderBottomLeftRadius = 8;
            dashboardCard.style.borderBottomRightRadius = 8;
            dashboardCard.style.borderTopWidth = 1;
            dashboardCard.style.borderBottomWidth = 1;
            dashboardCard.style.borderLeftWidth = 1;
            dashboardCard.style.borderRightWidth = 1;
            dashboardCard.style.borderTopColor = new Color(0.22f, 0.25f, 0.38f);
            dashboardCard.style.borderBottomColor = new Color(0.22f, 0.25f, 0.38f);
            dashboardCard.style.borderLeftColor = new Color(0.22f, 0.25f, 0.38f);
            dashboardCard.style.borderRightColor = new Color(0.22f, 0.25f, 0.38f);
            dashboardCard.style.paddingLeft = 14;
            dashboardCard.style.paddingRight = 14;
            dashboardCard.style.paddingTop = 12;
            dashboardCard.style.paddingBottom = 12;
            dashboardCard.style.marginBottom = 12;

            var statRow = new VisualElement();
            statRow.style.flexDirection = FlexDirection.Row;
            statRow.style.justifyContent = Justify.SpaceBetween;
            statRow.style.marginBottom = 10;

            CreateParamStatBadge(statRow, "总已用网络预算", $"{result.totalUsedBits} / 256 bits", result.barColor);
            int remain = Mathf.Max(0, 256 - result.totalUsedBits);
            CreateParamStatBadge(statRow, "剩余可用预算", $"{remain} bits", result.isExceeded ? new Color(0.95f, 0.3f, 0.3f) : new Color(0.38f, 0.75f, 0.98f));
            CreateParamStatBadge(statRow, "Avatar 基础占用", $"{result.baseBits} bits", new Color(0.6f, 0.65f, 0.8f));
            CreateParamStatBadge(statRow, "ND 菜单扩展占用", $"{result.addedBits} bits", new Color(0.75f, 0.55f, 0.95f));

            dashboardCard.Add(statRow);

            var progressBg = new VisualElement();
            progressBg.style.height = 14;
            progressBg.style.backgroundColor = new Color(0.08f, 0.09f, 0.14f);
            progressBg.style.borderTopLeftRadius = 7;
            progressBg.style.borderTopRightRadius = 7;
            progressBg.style.borderBottomLeftRadius = 7;
            progressBg.style.borderBottomRightRadius = 7;
            progressBg.style.overflow = Overflow.Hidden;
            progressBg.style.marginBottom = 6;

            var progressBarFill = new VisualElement();
            progressBarFill.style.height = Length.Percent(100);
            progressBarFill.style.width = Length.Percent(result.percentage * 100f);
            progressBarFill.style.backgroundColor = result.barColor;
            progressBg.Add(progressBarFill);
            dashboardCard.Add(progressBg);

            var progressText = new Label(result.statusText);
            progressText.style.fontSize = 11.5f;
            progressText.style.color = new Color(0.7f, 0.75f, 0.85f);
            dashboardCard.Add(progressText);

            paramInspectorViewContainer.Add(dashboardCard);

            // 2. Filter & Search Toolbar
            var filterRow = new VisualElement();
            filterRow.style.flexDirection = FlexDirection.Row;
            filterRow.style.justifyContent = Justify.SpaceBetween;
            filterRow.style.alignItems = Align.Center;
            filterRow.style.marginBottom = 10;

            var searchField = new TextField { placeholderText = "搜索参数名称 / 所属路径...", value = paramSearchKeyword };
            searchField.style.width = 280;
            searchField.RegisterValueChangedCallback(evt =>
            {
                paramSearchKeyword = evt.newValue?.Trim().ToLower() ?? "";
                RenderParamInspectorView();
            });

            var filterBtnGroup = new VisualElement();
            filterBtnGroup.style.flexDirection = FlexDirection.Row;

            AddParamFilterBtn(filterBtnGroup, "全部参数", 0);
            AddParamFilterBtn(filterBtnGroup, "网络同步", 1);
            AddParamFilterBtn(filterBtnGroup, "基础参数", 2);
            AddParamFilterBtn(filterBtnGroup, "ND 菜单控制项", 3);
            AddParamFilterBtn(filterBtnGroup, "优化诊断建议", 4);

            var openStandaloneBtn = new Button(() => NDParameterInspectorWindow.Open(currentAvatar)) { text = "独立视窗打开" };
            openStandaloneBtn.AddToClassList("preset-btn");
            openStandaloneBtn.style.marginLeft = 8;
            filterBtnGroup.Add(openStandaloneBtn);

            filterRow.Add(searchField);
            filterRow.Add(filterBtnGroup);
            paramInspectorViewContainer.Add(filterRow);

            // 3. Data Table Card
            var tableCard = new VisualElement();
            tableCard.style.flexGrow = 1;
            tableCard.style.backgroundColor = new Color(0.10f, 0.11f, 0.17f);
            tableCard.style.borderTopLeftRadius = 6;
            tableCard.style.borderTopRightRadius = 6;
            tableCard.style.borderBottomLeftRadius = 6;
            tableCard.style.borderBottomRightRadius = 6;
            tableCard.style.borderTopWidth = 1;
            tableCard.style.borderBottomWidth = 1;
            tableCard.style.borderLeftWidth = 1;
            tableCard.style.borderRightWidth = 1;
            tableCard.style.borderTopColor = new Color(0.20f, 0.22f, 0.34f);
            tableCard.style.borderBottomColor = new Color(0.20f, 0.22f, 0.34f);
            tableCard.style.borderLeftColor = new Color(0.20f, 0.22f, 0.34f);
            tableCard.style.borderRightColor = new Color(0.20f, 0.22f, 0.34f);
            tableCard.style.overflow = Overflow.Hidden;

            var tableHeader = new VisualElement();
            tableHeader.style.flexDirection = FlexDirection.Row;
            tableHeader.style.alignItems = Align.Center;
            tableHeader.style.backgroundColor = new Color(0.14f, 0.16f, 0.24f);
            tableHeader.style.height = 32;
            tableHeader.style.paddingLeft = 10;
            tableHeader.style.paddingRight = 10;
            tableHeader.style.borderBottomWidth = 1;
            tableHeader.style.borderBottomColor = new Color(0.22f, 0.25f, 0.38f);

            CreateParamHeaderCol(tableHeader, "类型 / 消耗", 105);
            CreateParamHeaderCol(tableHeader, "参数名称 (Parameter Name)", 200);
            CreateParamHeaderCol(tableHeader, "网络同步", 85);
            CreateParamHeaderCol(tableHeader, "记忆保存", 75);
            CreateParamHeaderCol(tableHeader, "默认值", 70);
            CreateParamHeaderCol(tableHeader, "所属来源 (分类与路径)", 230);
            CreateParamHeaderCol(tableHeader, "诊断与操作", 120);
            tableCard.Add(tableHeader);

            var tableScrollView = new ScrollView();
            tableScrollView.style.flexGrow = 1;
            tableCard.Add(tableScrollView);

            if (result.parameters != null && result.parameters.Count > 0)
            {
                var list = result.parameters.AsEnumerable();
                switch (paramFilterIndex)
                {
                    case 1: list = list.Where(p => p.IsSynced); break;
                    case 2: list = list.Where(p => p.Category.Contains("基础参数")); break;
                    case 3: list = list.Where(p => p.Category.Contains("ND 菜单")); break;
                    case 4: list = list.Where(p => p.IsOptimizable); break;
                }

                if (!string.IsNullOrEmpty(paramSearchKeyword))
                {
                    list = list.Where(p => p.Name.ToLower().Contains(paramSearchKeyword) || p.SourcePath.ToLower().Contains(paramSearchKeyword) || p.Category.ToLower().Contains(paramSearchKeyword));
                }

                int index = 0;
                foreach (var p in list)
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.minHeight = 32;
                    row.style.paddingLeft = 10;
                    row.style.paddingRight = 10;
                    row.style.backgroundColor = (index % 2 == 0) ? new Color(0.10f, 0.11f, 0.17f) : new Color(0.12f, 0.13f, 0.20f);
                    row.style.borderBottomWidth = 1;
                    row.style.borderBottomColor = new Color(0.16f, 0.18f, 0.27f);

                    var col1 = new VisualElement { style = { width = 105, flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    var badge = new Label();
                    badge.style.fontSize = 11;
                    badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                    badge.style.paddingLeft = 6;
                    badge.style.paddingRight = 6;
                    badge.style.paddingTop = 2;
                    badge.style.paddingBottom = 2;
                    badge.style.borderTopLeftRadius = 4;
                    badge.style.borderTopRightRadius = 4;
                    badge.style.borderBottomLeftRadius = 4;
                    badge.style.borderBottomRightRadius = 4;

                    if (!p.IsSynced)
                    {
                        badge.text = "0b 本地";
                        badge.style.backgroundColor = new Color(0.2f, 0.22f, 0.28f);
                        badge.style.color = new Color(0.6f, 0.65f, 0.75f);
                    }
                    else if (p.ValueType == "Bool")
                    {
                        badge.text = "1b Bool";
                        badge.style.backgroundColor = new Color(0.12f, 0.32f, 0.55f);
                        badge.style.color = new Color(0.4f, 0.85f, 1.0f);
                    }
                    else if (p.ValueType == "Int")
                    {
                        badge.text = "8b Int";
                        badge.style.backgroundColor = new Color(0.35f, 0.15f, 0.55f);
                        badge.style.color = new Color(0.85f, 0.6f, 1.0f);
                    }
                    else
                    {
                        badge.text = "8b Float";
                        badge.style.backgroundColor = new Color(0.55f, 0.3f, 0.1f);
                        badge.style.color = new Color(1.0f, 0.75f, 0.4f);
                    }
                    col1.Add(badge);
                    row.Add(col1);

                    var col2 = new Label(p.Name)
                    {
                        style = { width = 200, fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.9f, 0.93f, 0.98f) }
                    };
                    row.Add(col2);

                    var col3 = new Label(p.IsSynced ? "网络同步" : "仅本地")
                    {
                        style = { width = 85, fontSize = 11, color = p.IsSynced ? new Color(0.35f, 0.85f, 0.55f) : new Color(0.5f, 0.55f, 0.65f) }
                    };
                    row.Add(col3);

                    var col4 = new Label(p.IsSaved ? "记忆保存" : "不保存")
                    {
                        style = { width = 75, fontSize = 11, color = p.IsSaved ? new Color(0.7f, 0.75f, 0.9f) : new Color(0.5f, 0.55f, 0.65f) }
                    };
                    row.Add(col4);

                    var col5 = new Label(p.DefaultValue)
                    {
                        style = { width = 70, fontSize = 11, color = new Color(0.85f, 0.88f, 0.95f) }
                    };
                    row.Add(col5);

                    var col6 = new VisualElement { style = { width = 230, flexDirection = FlexDirection.Column, justifyContent = Justify.Center } };
                    var catLabel = new Label(p.Category) { style = { fontSize = 10, color = new Color(0.5f, 0.55f, 0.7f), marginBottom = 1 } };
                    var pathLabel = new Label(p.SourcePath) { style = { fontSize = 11, color = new Color(0.75f, 0.8f, 0.9f) } };
                    col6.Add(catLabel);
                    col6.Add(pathLabel);
                    row.Add(col6);

                    var col7 = new VisualElement { style = { width = 120, flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    var targetObj = p.SourceComponent ?? (UnityEngine.Object)p.SourceGameObject;
                    if (targetObj != null)
                    {
                        var pingBtn = new Label("定位");
                        pingBtn.AddToClassList("part-chip-preview");
                        pingBtn.tooltip = "在 Hierarchy / Project 中高亮定位此组件";
                        pingBtn.style.paddingLeft = 6;
                        pingBtn.style.paddingRight = 6;
                        pingBtn.RegisterCallback<ClickEvent>(_ =>
                        {
                            EditorGUIUtility.PingObject(targetObj);
                            if (p.SourceGameObject != null) Selection.activeGameObject = p.SourceGameObject;
                        });
                        col7.Add(pingBtn);
                    }

                    if (p.SourceGameObject != null)
                    {
                        var prevBtn = new Label("预览");
                        prevBtn.AddToClassList("part-chip-add");
                        prevBtn.tooltip = "打开 3D 独立窗口查看对应模型";
                        prevBtn.style.paddingLeft = 6;
                        prevBtn.style.paddingRight = 6;
                        prevBtn.RegisterCallback<ClickEvent>(_ => NDItemPreviewWindow.ShowPreview(p.SourceGameObject));
                        col7.Add(prevBtn);
                    }

                    if (p.IsOptimizable)
                    {
                        var optBadge = new Label("可优化");
                        optBadge.style.fontSize = 10;
                        optBadge.style.backgroundColor = new Color(0.55f, 0.4f, 0.05f);
                        optBadge.style.color = new Color(1.0f, 0.9f, 0.4f);
                        optBadge.style.paddingLeft = 4;
                        optBadge.style.paddingRight = 4;
                        optBadge.style.borderTopLeftRadius = 3;
                        optBadge.style.borderTopRightRadius = 3;
                        optBadge.style.borderBottomLeftRadius = 3;
                        optBadge.style.borderBottomRightRadius = 3;
                        optBadge.style.marginLeft = 4;
                        optBadge.tooltip = p.OptimizationTip;
                        col7.Add(optBadge);
                    }

                    row.Add(col7);
                    tableScrollView.Add(row);
                    index++;
                }
            }
            else
            {
                var empty = new Label(currentAvatar == null ? "请先选择目标 Avatar 模型" : "未扫描到任何表达参数");
                empty.style.paddingTop = 40;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.color = new Color(0.5f, 0.55f, 0.65f);
                tableScrollView.Add(empty);
            }

            paramInspectorViewContainer.Add(tableCard);
        }

        private Label CreateParamStatBadge(VisualElement parent, string title, string val, Color valColor)
        {
            var box = new VisualElement();
            box.style.alignItems = Align.Center;

            var tLbl = new Label(title);
            tLbl.style.fontSize = 11;
            tLbl.style.color = new Color(0.55f, 0.6f, 0.72f);
            tLbl.style.marginBottom = 2;

            var vLbl = new Label(val);
            vLbl.style.fontSize = 15;
            vLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            vLbl.style.color = valColor;

            box.Add(tLbl);
            box.Add(vLbl);
            parent.Add(box);
            return vLbl;
        }

        private void AddParamFilterBtn(VisualElement parent, string text, int filterIdx)
        {
            var btn = new Button(() =>
            {
                paramFilterIndex = filterIdx;
                RenderParamInspectorView();
            }) { text = text };
            btn.style.height = 24;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.fontSize = 11;
            btn.style.backgroundColor = (paramFilterIndex == filterIdx) ? new Color(0.25f, 0.35f, 0.65f) : new Color(0.16f, 0.18f, 0.27f);
            btn.style.color = Color.white;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.marginLeft = 4;
            parent.Add(btn);
        }

        private void CreateParamHeaderCol(VisualElement parent, string text, float width)
        {
            var lbl = new Label(text);
            lbl.style.width = width;
            lbl.style.fontSize = 11.5f;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new Color(0.65f, 0.7f, 0.85f);
            parent.Add(lbl);
        }

        #endregion
    }
}
