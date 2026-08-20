using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace Custom.NDMenuManager.Editor.UI
{
    public class NDParameterInspectorWindow : EditorWindow
    {
        private VRCAvatarDescriptor currentAvatar;
        private ObjectField avatarField;
        private Label totalBitsBadge;
        private Label remainingBitsBadge;
        private Label baseBitsBadge;
        private Label addedBitsBadge;
        private VisualElement progressBarFill;
        private Label progressText;
        private TextField searchField;
        private ScrollView tableScrollView;

        private enum FilterCategory { All, SyncedOnly, BaseOnly, NDMenuOnly, OptimizableOnly }
        private FilterCategory currentFilter = FilterCategory.All;
        private string searchKeyword = "";
        private BitBudgetResult currentResult;

        [MenuItem("Tools/psenY7 ND Menu Manager/参数预算与详情总览", priority = 102)]
        public static void Open()
        {
            var win = GetWindow<NDParameterInspectorWindow>("参数预算与详情", true);
            win.minSize = new Vector2(850, 600);
            win.Show();
        }

        public static void Open(VRCAvatarDescriptor avatar)
        {
            var win = GetWindow<NDParameterInspectorWindow>("参数预算与详情", true);
            win.minSize = new Vector2(850, 600);
            win.Show();
            if (avatar != null)
            {
                win.SetAvatar(avatar);
            }
        }

        public void SetAvatar(VRCAvatarDescriptor avatar)
        {
            currentAvatar = avatar;
            if (avatarField != null)
            {
                avatarField.value = currentAvatar != null ? currentAvatar.gameObject : null;
            }
            RefreshData();
        }

        private void OnEnable()
        {
            if (currentAvatar == null)
            {
                currentAvatar = FindObjectOfType<VRCAvatarDescriptor>();
            }
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = new Color(0.08f, 0.09f, 0.13f);
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;

            // 1. Top Header Bar
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.alignItems = Align.Center;
            topBar.style.marginBottom = 14;

            var titleLabel = new Label("Avatar 表达参数 (Expression Parameters) 预算与详情");
            titleLabel.style.fontSize = 16;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.95f, 0.96f, 0.98f);

            var topActions = new VisualElement();
            topActions.style.flexDirection = FlexDirection.Row;
            topActions.style.alignItems = Align.Center;

            avatarField = new ObjectField("目标 Avatar:")
            {
                objectType = typeof(GameObject),
                value = currentAvatar != null ? currentAvatar.gameObject : null
            };
            avatarField.style.width = 240;
            avatarField.RegisterValueChangedCallback(evt =>
            {
                var go = evt.newValue as GameObject;
                currentAvatar = go != null ? go.GetComponent<VRCAvatarDescriptor>() : null;
                RefreshData();
            });

            var refreshBtn = new Button(RefreshData) { text = "刷新扫描" };
            refreshBtn.style.height = 26;
            refreshBtn.style.paddingLeft = 10;
            refreshBtn.style.paddingRight = 10;
            refreshBtn.style.backgroundColor = new Color(0.2f, 0.22f, 0.32f);
            refreshBtn.style.color = Color.white;
            refreshBtn.style.borderTopLeftRadius = 4;
            refreshBtn.style.borderTopRightRadius = 4;
            refreshBtn.style.borderBottomLeftRadius = 4;
            refreshBtn.style.borderBottomRightRadius = 4;
            refreshBtn.style.marginLeft = 8;

            var copyReportBtn = new Button(ExportReport) { text = "复制报告" };
            copyReportBtn.style.height = 26;
            copyReportBtn.style.paddingLeft = 10;
            copyReportBtn.style.paddingRight = 10;
            copyReportBtn.style.backgroundColor = new Color(0.18f, 0.35f, 0.65f);
            copyReportBtn.style.color = Color.white;
            copyReportBtn.style.borderTopLeftRadius = 4;
            copyReportBtn.style.borderTopRightRadius = 4;
            copyReportBtn.style.borderBottomLeftRadius = 4;
            copyReportBtn.style.borderBottomRightRadius = 4;
            copyReportBtn.style.marginLeft = 6;

            topActions.Add(avatarField);
            topActions.Add(refreshBtn);
            topActions.Add(copyReportBtn);

            topBar.Add(titleLabel);
            topBar.Add(topActions);
            root.Add(topBar);

            // 2. Budget Dashboard Card
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

            // Stat numbers row
            var statRow = new VisualElement();
            statRow.style.flexDirection = FlexDirection.Row;
            statRow.style.justifyContent = Justify.SpaceBetween;
            statRow.style.marginBottom = 10;

            totalBitsBadge = CreateStatBadge(statRow, "总已用网络预算", "0 / 256 bits", new Color(0.24f, 0.70f, 0.44f));
            remainingBitsBadge = CreateStatBadge(statRow, "剩余可用预算", "256 bits", new Color(0.38f, 0.75f, 0.98f));
            baseBitsBadge = CreateStatBadge(statRow, "Avatar 基础占用", "0 bits", new Color(0.6f, 0.65f, 0.8f));
            addedBitsBadge = CreateStatBadge(statRow, "ND 菜单扩展占用", "0 bits", new Color(0.75f, 0.55f, 0.95f));

            dashboardCard.Add(statRow);

            // Progress Bar
            var progressBg = new VisualElement();
            progressBg.style.height = 14;
            progressBg.style.backgroundColor = new Color(0.08f, 0.09f, 0.14f);
            progressBg.style.borderTopLeftRadius = 7;
            progressBg.style.borderTopRightRadius = 7;
            progressBg.style.borderBottomLeftRadius = 7;
            progressBg.style.borderBottomRightRadius = 7;
            progressBg.style.overflow = Overflow.Hidden;
            progressBg.style.marginBottom = 6;

            progressBarFill = new VisualElement();
            progressBarFill.style.height = Length.Percent(100);
            progressBarFill.style.width = Length.Percent(0);
            progressBarFill.style.backgroundColor = new Color(0.24f, 0.70f, 0.44f);
            progressBg.Add(progressBarFill);
            dashboardCard.Add(progressBg);

            progressText = new Label("未加载 Avatar");
            progressText.style.fontSize = 11.5f;
            progressText.style.color = new Color(0.7f, 0.75f, 0.85f);
            dashboardCard.Add(progressText);

            root.Add(dashboardCard);

            // 3. Search and Filters Toolbar
            var filterRow = new VisualElement();
            filterRow.style.flexDirection = FlexDirection.Row;
            filterRow.style.justifyContent = Justify.SpaceBetween;
            filterRow.style.alignItems = Align.Center;
            filterRow.style.marginBottom = 10;

            searchField = new TextField("搜索参数:");
            searchField.style.width = 280;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchKeyword = evt.newValue?.Trim().ToLower() ?? "";
                RenderTable();
            });

            var filterBtnGroup = new VisualElement();
            filterBtnGroup.style.flexDirection = FlexDirection.Row;

            AddFilterButton(filterBtnGroup, "全部参数", FilterCategory.All);
            AddFilterButton(filterBtnGroup, "网络同步 (1b/8b)", FilterCategory.SyncedOnly);
            AddFilterButton(filterBtnGroup, "基础参数", FilterCategory.BaseOnly);
            AddFilterButton(filterBtnGroup, "ND 菜单控制项", FilterCategory.NDMenuOnly);
            AddFilterButton(filterBtnGroup, "优化诊断建议", FilterCategory.OptimizableOnly);

            filterRow.Add(searchField);
            filterRow.Add(filterBtnGroup);
            root.Add(filterRow);

            // 4. Data Table (Headers + Scrollable Content)
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

            // Table Header Row
            var tableHeader = new VisualElement();
            tableHeader.style.flexDirection = FlexDirection.Row;
            tableHeader.style.alignItems = Align.Center;
            tableHeader.style.backgroundColor = new Color(0.14f, 0.16f, 0.24f);
            tableHeader.style.height = 32;
            tableHeader.style.paddingLeft = 10;
            tableHeader.style.paddingRight = 10;
            tableHeader.style.borderBottomWidth = 1;
            tableHeader.style.borderBottomColor = new Color(0.22f, 0.25f, 0.38f);

            CreateHeaderCol(tableHeader, "类型 / 消耗", 105);
            CreateHeaderCol(tableHeader, "参数名称 (Parameter Name)", 200);
            CreateHeaderCol(tableHeader, "网络同步", 85);
            CreateHeaderCol(tableHeader, "记忆保存", 75);
            CreateHeaderCol(tableHeader, "默认值", 70);
            CreateHeaderCol(tableHeader, "所属来源 (分类与路径)", 230);
            CreateHeaderCol(tableHeader, "诊断与操作", 120);

            tableCard.Add(tableHeader);

            tableScrollView = new ScrollView();
            tableScrollView.style.flexGrow = 1;
            tableCard.Add(tableScrollView);

            root.Add(tableCard);

            RefreshData();
        }

        private Label CreateStatBadge(VisualElement parent, string title, string val, Color valColor)
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

        private void AddFilterButton(VisualElement parent, string text, FilterCategory cat)
        {
            var btn = new Button(() =>
            {
                currentFilter = cat;
                RenderTable();
            }) { text = text };
            btn.style.height = 24;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.fontSize = 11;
            btn.style.backgroundColor = new Color(0.16f, 0.18f, 0.27f);
            btn.style.color = new Color(0.8f, 0.85f, 0.95f);
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.marginLeft = 4;
            parent.Add(btn);
        }

        private void CreateHeaderCol(VisualElement parent, string text, float width)
        {
            var lbl = new Label(text);
            lbl.style.width = width;
            lbl.style.fontSize = 11.5f;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new Color(0.65f, 0.7f, 0.85f);
            parent.Add(lbl);
        }

        public void RefreshData()
        {
            if (currentAvatar == null)
            {
                currentAvatar = FindObjectOfType<VRCAvatarDescriptor>();
            }

            if (currentAvatar != null)
            {
                currentResult = BitBudgetCalculator.Calculate(currentAvatar);

                if (totalBitsBadge != null) totalBitsBadge.text = $"{currentResult.totalUsedBits} / 256 bits";
                if (remainingBitsBadge != null)
                {
                    int remain = Mathf.Max(0, 256 - currentResult.totalUsedBits);
                    remainingBitsBadge.text = $"{remain} bits";
                    remainingBitsBadge.style.color = currentResult.isExceeded ? new Color(0.95f, 0.3f, 0.3f) : new Color(0.38f, 0.75f, 0.98f);
                }
                if (baseBitsBadge != null) baseBitsBadge.text = $"{currentResult.baseBits} bits";
                if (addedBitsBadge != null) addedBitsBadge.text = $"{currentResult.addedBits} bits";

                if (progressBarFill != null)
                {
                    progressBarFill.style.width = Length.Percent(currentResult.percentage * 100f);
                    progressBarFill.style.backgroundColor = currentResult.barColor;
                }

                if (progressText != null)
                {
                    progressText.text = currentResult.statusText;
                }
            }
            else
            {
                currentResult = default;
                if (totalBitsBadge != null) totalBitsBadge.text = "0 / 256 bits";
                if (remainingBitsBadge != null) remainingBitsBadge.text = "256 bits";
                if (baseBitsBadge != null) baseBitsBadge.text = "0 bits";
                if (addedBitsBadge != null) addedBitsBadge.text = "0 bits";
                if (progressBarFill != null) progressBarFill.style.width = Length.Percent(0);
                if (progressText != null) progressText.text = "请在上方指定目标 Avatar 模型";
            }

            RenderTable();
        }

        private void RenderTable()
        {
            if (tableScrollView == null) return;
            tableScrollView.Clear();

            if (currentResult.parameters == null || currentResult.parameters.Count == 0)
            {
                var empty = new Label(currentAvatar == null ? "请先选择目标 Avatar 模型" : "未扫描到任何表达参数");
                empty.style.paddingTop = 40;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.color = new Color(0.5f, 0.55f, 0.65f);
                tableScrollView.Add(empty);
                return;
            }

            var list = currentResult.parameters.AsEnumerable();

            // Filter
            switch (currentFilter)
            {
                case FilterCategory.SyncedOnly:
                    list = list.Where(p => p.IsSynced);
                    break;
                case FilterCategory.BaseOnly:
                    list = list.Where(p => p.Category.Contains("基础参数"));
                    break;
                case FilterCategory.NDMenuOnly:
                    list = list.Where(p => p.Category.Contains("ND 菜单"));
                    break;
                case FilterCategory.OptimizableOnly:
                    list = list.Where(p => p.IsOptimizable);
                    break;
            }

            // Search Keyword
            if (!string.IsNullOrEmpty(searchKeyword))
            {
                list = list.Where(p => p.Name.ToLower().Contains(searchKeyword) || p.SourcePath.ToLower().Contains(searchKeyword) || p.Category.ToLower().Contains(searchKeyword));
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

                // Col 1: Type / Cost Badge
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

                // Col 2: Name
                var col2 = new Label(p.Name)
                {
                    style =
                    {
                        width = 200,
                        fontSize = 12,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = new Color(0.9f, 0.93f, 0.98f)
                    }
                };
                row.Add(col2);

                // Col 3: Synced
                var col3 = new Label(p.IsSynced ? "网络同步" : "仅本地")
                {
                    style =
                    {
                        width = 85,
                        fontSize = 11,
                        color = p.IsSynced ? new Color(0.35f, 0.85f, 0.55f) : new Color(0.5f, 0.55f, 0.65f)
                    }
                };
                row.Add(col3);

                // Col 4: Saved
                var col4 = new Label(p.IsSaved ? "记忆保存" : "不保存")
                {
                    style =
                    {
                        width = 75,
                        fontSize = 11,
                        color = p.IsSaved ? new Color(0.7f, 0.75f, 0.9f) : new Color(0.5f, 0.55f, 0.65f)
                    }
                };
                row.Add(col4);

                // Col 5: Default Value
                var col5 = new Label(p.DefaultValue)
                {
                    style =
                    {
                        width = 70,
                        fontSize = 11,
                        color = new Color(0.85f, 0.88f, 0.95f)
                    }
                };
                row.Add(col5);

                // Col 6: Source & Category
                var col6 = new VisualElement { style = { width = 230, flexDirection = FlexDirection.Column, justifyContent = Justify.Center } };
                var catLabel = new Label(p.Category)
                {
                    style = { fontSize = 10, color = new Color(0.5f, 0.55f, 0.7f), marginBottom = 1 }
                };
                var pathLabel = new Label(p.SourcePath)
                {
                    style = { fontSize = 11, color = new Color(0.75f, 0.8f, 0.9f) }
                };
                col6.Add(catLabel);
                col6.Add(pathLabel);
                row.Add(col6);

                // Col 7: Actions (Ping / Preview / Optimize)
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

        private void ExportReport()
        {
            if (currentResult.parameters == null || currentResult.parameters.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "当前没有可导出的参数数据。", "确定");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# Avatar 表达参数预算与详情报告");
            sb.AppendLine($"- 模型名称: {(currentAvatar != null ? currentAvatar.gameObject.name : "未知")}");
            sb.AppendLine($"- 总已用网络预算: {currentResult.totalUsedBits} / 256 bits ({currentResult.percentage:P1})");
            sb.AppendLine($"- 基础参数: {currentResult.baseBits} bits | ND 扩展: {currentResult.addedBits} bits | 剩余: {256 - currentResult.totalUsedBits} bits");
            sb.AppendLine();
            sb.AppendLine("| 类型 | 参数名称 | 网络同步 | 记忆保存 | 默认值 | 分类与路径 | 优化建议 |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |");

            foreach (var p in currentResult.parameters)
            {
                string sync = p.IsSynced ? "网络同步" : "仅本地";
                string save = p.IsSaved ? "是" : "否";
                string opt = p.IsOptimizable ? p.OptimizationTip : "-";
                sb.AppendLine($"| {p.ValueType} ({p.BitCost}b) | `{p.Name}` | {sync} | {save} | {p.DefaultValue} | {p.Category} - {p.SourcePath} | {opt} |");
            }

            GUIUtility.systemCopyBuffer = sb.ToString();
            EditorUtility.DisplayDialog("复制成功", "参数预算完整 Markdown 报告已复制到您的剪贴板！", "确定");
        }
    }
}
