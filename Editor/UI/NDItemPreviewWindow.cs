using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Custom.NDMenuManager.Editor.Generators;

namespace Custom.NDMenuManager.Editor.UI
{
    public class NDItemPreviewWindow : EditorWindow
    {
        private GameObject currentTarget;
        private Texture2D previewTexture;
        private Image previewImage;
        private Label nameLabel;
        private Label pathLabel;
        private Label typeLabel;
        private Label meshInfoLabel;
        private Label materialInfoLabel;
        private Label statusBadge;

        [MenuItem("Tools/psenY7 ND Menu Manager/部件 3D 预览窗口", priority = 101)]
        public static void Open()
        {
            var win = GetWindow<NDItemPreviewWindow>("部件预览", true);
            win.minSize = new Vector2(380, 480);
            win.Show();
        }

        public static void ShowPreview(GameObject target)
        {
            if (target == null) return;

            var win = GetWindow<NDItemPreviewWindow>("部件预览", true);
            win.minSize = new Vector2(380, 480);
            win.Show();
            win.SetTarget(target);

            // Automatically ping and select in Unity Hierarchy
            EditorGUIUtility.PingObject(target);
            Selection.activeGameObject = target;
        }

        public void SetTarget(GameObject target)
        {
            currentTarget = target;
            if (currentTarget == null) return;

            titleContent = new GUIContent($"预览: {currentTarget.name}", EditorGUIUtility.IconContent("d_ViewToolOrbit").image);

            // Generate clean isolated 3D snapshot
            previewTexture = ThumbnailGenerator.CaptureGameObjectThumbnail(currentTarget, 256);

            UpdateUI();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = new Color(0.08f, 0.09f, 0.13f);
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;

            // 1. Header Row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 10;

            nameLabel = new Label("尚未选择部件");
            nameLabel.style.fontSize = 15;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.95f, 0.96f, 0.98f);

            statusBadge = new Label("未加载");
            statusBadge.style.fontSize = 11;
            statusBadge.style.paddingLeft = 8;
            statusBadge.style.paddingRight = 8;
            statusBadge.style.paddingTop = 2;
            statusBadge.style.paddingBottom = 2;
            statusBadge.style.borderTopLeftRadius = 10;
            statusBadge.style.borderTopRightRadius = 10;
            statusBadge.style.borderBottomLeftRadius = 10;
            statusBadge.style.borderBottomRightRadius = 10;
            statusBadge.style.backgroundColor = new Color(0.15f, 0.17f, 0.25f);
            statusBadge.style.color = new Color(0.6f, 0.7f, 0.8f);

            headerRow.Add(nameLabel);
            headerRow.Add(statusBadge);
            root.Add(headerRow);

            // 2. 3D Preview Image Card
            var previewCard = new VisualElement();
            previewCard.style.height = 190;
            previewCard.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            previewCard.style.borderTopWidth = 1;
            previewCard.style.borderBottomWidth = 1;
            previewCard.style.borderLeftWidth = 1;
            previewCard.style.borderRightWidth = 1;
            previewCard.style.borderTopColor = new Color(0.18f, 0.2f, 0.3f);
            previewCard.style.borderBottomColor = new Color(0.18f, 0.2f, 0.3f);
            previewCard.style.borderLeftColor = new Color(0.18f, 0.2f, 0.3f);
            previewCard.style.borderRightColor = new Color(0.18f, 0.2f, 0.3f);
            previewCard.style.borderTopLeftRadius = 8;
            previewCard.style.borderTopRightRadius = 8;
            previewCard.style.borderBottomLeftRadius = 8;
            previewCard.style.borderBottomRightRadius = 8;
            previewCard.style.justifyContent = Justify.Center;
            previewCard.style.alignItems = Align.Center;
            previewCard.style.marginBottom = 12;
            previewCard.style.overflow = Overflow.Hidden;

            previewImage = new Image();
            previewImage.style.width = 180;
            previewImage.style.height = 180;
            previewImage.scaleMode = ScaleMode.ScaleToFit;
            previewCard.Add(previewImage);
            root.Add(previewCard);

            // 3. Metadata Info Box
            var infoBox = new VisualElement();
            infoBox.style.backgroundColor = new Color(0.11f, 0.12f, 0.18f);
            infoBox.style.borderTopLeftRadius = 6;
            infoBox.style.borderTopRightRadius = 6;
            infoBox.style.borderBottomLeftRadius = 6;
            infoBox.style.borderBottomRightRadius = 6;
            infoBox.style.paddingLeft = 10;
            infoBox.style.paddingRight = 10;
            infoBox.style.paddingTop = 8;
            infoBox.style.paddingBottom = 8;
            infoBox.style.marginBottom = 12;

            pathLabel = CreateInfoRow(infoBox, "层级路径:", "-");
            typeLabel = CreateInfoRow(infoBox, "组件类型:", "-");
            meshInfoLabel = CreateInfoRow(infoBox, "网格信息:", "-");
            materialInfoLabel = CreateInfoRow(infoBox, "材质球:", "-");

            root.Add(infoBox);

            // 4. Action Buttons Toolbar
            var actionRow1 = new VisualElement();
            actionRow1.style.flexDirection = FlexDirection.Row;
            actionRow1.style.marginBottom = 6;

            var pingBtn = new Button(OnPingClicked) { text = "在 Hierarchy 中定位" };
            pingBtn.style.flexGrow = 1;
            pingBtn.style.height = 30;
            pingBtn.style.backgroundColor = new Color(0.2f, 0.25f, 0.4f);
            pingBtn.style.color = Color.white;
            pingBtn.style.borderTopLeftRadius = 4;
            pingBtn.style.borderTopRightRadius = 4;
            pingBtn.style.borderBottomLeftRadius = 4;
            pingBtn.style.borderBottomRightRadius = 4;
            pingBtn.style.marginRight = 6;

            var focusBtn = new Button(OnFocusClicked) { text = "在 Scene 中聚焦" };
            focusBtn.style.flexGrow = 1;
            focusBtn.style.height = 30;
            focusBtn.style.backgroundColor = new Color(0.15f, 0.35f, 0.7f);
            focusBtn.style.color = Color.white;
            focusBtn.style.borderTopLeftRadius = 4;
            focusBtn.style.borderTopRightRadius = 4;
            focusBtn.style.borderBottomLeftRadius = 4;
            focusBtn.style.borderBottomRightRadius = 4;

            actionRow1.Add(pingBtn);
            actionRow1.Add(focusBtn);
            root.Add(actionRow1);

            var actionRow2 = new VisualElement();
            actionRow2.style.flexDirection = FlexDirection.Row;

            var blinkBtn = new Button(OnBlinkClicked) { text = "场景中闪烁显隐 (确认位置)" };
            blinkBtn.style.flexGrow = 1;
            blinkBtn.style.height = 28;
            blinkBtn.style.backgroundColor = new Color(0.18f, 0.2f, 0.3f);
            blinkBtn.style.color = new Color(0.9f, 0.9f, 0.95f);
            blinkBtn.style.borderTopLeftRadius = 4;
            blinkBtn.style.borderTopRightRadius = 4;
            blinkBtn.style.borderBottomLeftRadius = 4;
            blinkBtn.style.borderBottomRightRadius = 4;
            blinkBtn.style.marginRight = 6;

            var toggleActiveBtn = new Button(OnToggleActiveClicked) { text = "切换显隐状态" };
            toggleActiveBtn.style.flexGrow = 1;
            toggleActiveBtn.style.height = 28;
            toggleActiveBtn.style.backgroundColor = new Color(0.18f, 0.2f, 0.3f);
            toggleActiveBtn.style.color = new Color(0.9f, 0.9f, 0.95f);
            toggleActiveBtn.style.borderTopLeftRadius = 4;
            toggleActiveBtn.style.borderTopRightRadius = 4;
            toggleActiveBtn.style.borderBottomLeftRadius = 4;
            toggleActiveBtn.style.borderBottomRightRadius = 4;

            actionRow2.Add(blinkBtn);
            actionRow2.Add(toggleActiveBtn);
            root.Add(actionRow2);

            UpdateUI();
        }

        private Label CreateInfoRow(VisualElement parent, string title, string defaultValue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            var titleLbl = new Label(title);
            titleLbl.style.width = 75;
            titleLbl.style.fontSize = 11.5f;
            titleLbl.style.color = new Color(0.55f, 0.6f, 0.75f);
            row.Add(titleLbl);

            var valLbl = new Label(defaultValue);
            valLbl.style.flexGrow = 1;
            valLbl.style.fontSize = 11.5f;
            valLbl.style.color = new Color(0.85f, 0.88f, 0.95f);
            row.Add(valLbl);

            parent.Add(row);
            return valLbl;
        }

        private void UpdateUI()
        {
            if (nameLabel == null) return;

            if (currentTarget == null)
            {
                nameLabel.text = "尚未选择任何物体";
                if (statusBadge != null) statusBadge.text = "空";
                if (previewImage != null) previewImage.image = null;
                if (pathLabel != null) pathLabel.text = "-";
                if (typeLabel != null) typeLabel.text = "-";
                if (meshInfoLabel != null) meshInfoLabel.text = "-";
                if (materialInfoLabel != null) materialInfoLabel.text = "-";
                return;
            }

            nameLabel.text = currentTarget.name;

            bool isActive = currentTarget.activeSelf;
            if (statusBadge != null)
            {
                statusBadge.text = isActive ? "处于启用状态" : "处于隐藏状态";
                statusBadge.style.backgroundColor = isActive ? new Color(0.1f, 0.35f, 0.2f) : new Color(0.35f, 0.15f, 0.15f);
                statusBadge.style.color = isActive ? new Color(0.4f, 0.95f, 0.6f) : new Color(0.95f, 0.4f, 0.4f);
            }

            if (previewImage != null)
            {
                previewImage.image = previewTexture;
            }

            if (pathLabel != null)
            {
                pathLabel.text = GetBreadcrumbPath(currentTarget);
            }

            Renderer rend = currentTarget.GetComponent<Renderer>();
            if (typeLabel != null)
            {
                if (rend is SkinnedMeshRenderer) typeLabel.text = "SkinnedMeshRenderer (蒙皮网格)";
                else if (rend is MeshRenderer) typeLabel.text = "MeshRenderer (静态网格)";
                else typeLabel.text = "GameObject (容器对象)";
            }

            if (meshInfoLabel != null)
            {
                Mesh mesh = null;
                if (rend is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else if (currentTarget.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;

                if (mesh != null)
                {
                    meshInfoLabel.text = $"{mesh.vertexCount} 顶点 | {mesh.triangles.Length / 3} 三角形";
                }
                else
                {
                    meshInfoLabel.text = "无网格数据";
                }
            }

            if (materialInfoLabel != null)
            {
                if (rend != null && rend.sharedMaterials != null && rend.sharedMaterials.Length > 0)
                {
                    var mats = new List<string>();
                    foreach (var m in rend.sharedMaterials)
                    {
                        if (m != null) mats.Add(m.name);
                    }
                    materialInfoLabel.text = mats.Count > 0 ? string.Join(", ", mats) : "无材质球";
                }
                else
                {
                    materialInfoLabel.text = "无材质";
                }
            }
        }

        private string GetBreadcrumbPath(GameObject go)
        {
            var parts = new List<string>();
            var curr = go.transform;
            while (curr != null)
            {
                parts.Insert(0, curr.name);
                curr = curr.parent;
            }
            return string.Join(" / ", parts);
        }

        private void OnPingClicked()
        {
            if (currentTarget == null) return;
            EditorGUIUtility.PingObject(currentTarget);
            Selection.activeGameObject = currentTarget;
        }

        private void OnFocusClicked()
        {
            if (currentTarget == null) return;
            EditorGUIUtility.PingObject(currentTarget);
            Selection.activeGameObject = currentTarget;

            var rend = currentTarget.GetComponentInChildren<Renderer>(true);
            Bounds bounds = rend != null ? rend.bounds : new Bounds(currentTarget.transform.position, Vector3.one * 0.5f);

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(bounds, false);
            }
        }

        private void OnToggleActiveClicked()
        {
            if (currentTarget == null) return;
            Undo.RecordObject(currentTarget, "Toggle Active");
            currentTarget.SetActive(!currentTarget.activeSelf);
            UpdateUI();
        }

        private void OnBlinkClicked()
        {
            if (currentTarget == null) return;
            EditorCoroutineRunner.StartCoroutine(BlinkRoutine(currentTarget));
        }

        private static IEnumerator BlinkRoutine(GameObject go)
        {
            if (go == null) yield break;
            bool original = go.activeSelf;

            for (int i = 0; i < 3; i++)
            {
                go.SetActive(!original);
                yield return new WaitForSeconds(0.18f);
                go.SetActive(original);
                yield return new WaitForSeconds(0.18f);
            }
        }
    }

    public static class EditorCoroutineRunner
    {
        public static void StartCoroutine(IEnumerator routine)
        {
            EditorApplication.CallbackFunction updateAction = null;
            updateAction = () =>
            {
                try
                {
                    if (routine.MoveNext())
                    {
                        // continue next frame
                    }
                    else
                    {
                        EditorApplication.update -= updateAction;
                    }
                }
                catch (Exception)
                {
                    EditorApplication.update -= updateAction;
                }
            };
            EditorApplication.update += updateAction;
        }
    }
}
