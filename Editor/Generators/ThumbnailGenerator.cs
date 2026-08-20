using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Custom.NDMenuManager.Editor.Generators
{
    public static class ThumbnailGenerator
    {
        private const int ThumbnailResolution = 256;
        private const string ThumbnailLayerName = "ND_Thumbnail";
        private const float CameraFov = 40.0f;
        private const int StartingUserLayer = 8;
        private const int MaxLayers = 32;

        /// <summary>
        /// Captures an isolated, studio-lit, auto-centered 3D thumbnail of ONLY the target object.
        /// Body, face, and other clothing parts in the scene are completely excluded.
        /// </summary>
        public static Texture2D CaptureGameObjectThumbnail(GameObject target, int resolution = ThumbnailResolution)
        {
            if (target == null) return null;

            EnsureThumbnailLayer();

            int layerIndex = LayerMask.NameToLayer(ThumbnailLayerName);
            if (layerIndex < 0) layerIndex = 0; // Fallback

            RenderTexture rt = null;
            GameObject camGo = null;
            GameObject clone = null;
            GameObject keyLightGo = null;
            GameObject fillLightGo = null;
            Texture2D texture = null;

            try
            {
                // 1. Create an isolated clone far away from the scene (isolated world space)
                clone = Object.Instantiate(target);
                clone.name = "ND_Temp_ThumbnailClone";
                clone.transform.position = new Vector3(0, -5000, 0);
                clone.SetActive(true);

                // Enable all renderers on the isolated clone
                foreach (var rend in clone.GetComponentsInChildren<Renderer>(true))
                {
                    rend.enabled = true;
                }

                // Assign isolated thumbnail layer exclusively to clone and all its children
                RecursiveSetLayer(clone, layerIndex);

                // 2. Calculate accurate bounds of ONLY this isolated target
                var renderers = clone.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) return null;

                Bounds bounds = new Bounds();
                bool hasBounds = false;
                foreach (var r in renderers)
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }

                if (!hasBounds || bounds.size.sqrMagnitude < 0.0001f)
                {
                    bounds = new Bounds(clone.transform.position, Vector3.one * 0.2f);
                }

                // 3. Setup Camera that ONLY sees the isolated thumbnail layer
                rt = RenderTexture.GetTemporary(resolution, resolution, 24, RenderTextureFormat.ARGB32);
                camGo = new GameObject("ND_Temp_ThumbnailCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.targetTexture = rt;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0); // 100% Alpha Transparent
                cam.fieldOfView = CameraFov;
                cam.cullingMask = (layerIndex > 0) ? (1 << layerIndex) : ~0;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 100f;

                // 4. Setup 3-Point Studio Lighting
                keyLightGo = new GameObject("ND_Temp_KeyLight");
                var keyLight = keyLightGo.AddComponent<Light>();
                keyLight.type = LightType.Directional;
                keyLight.intensity = 1.4f;
                keyLight.color = Color.white;
                keyLightGo.transform.SetParent(camGo.transform, false);
                keyLightGo.transform.localRotation = Quaternion.Euler(30f, 35f, 0f);

                fillLightGo = new GameObject("ND_Temp_FillLight");
                var fillLight = fillLightGo.AddComponent<Light>();
                fillLight.type = LightType.Directional;
                fillLight.intensity = 0.7f;
                fillLight.color = new Color(0.85f, 0.92f, 1f);
                fillLightGo.transform.SetParent(camGo.transform, false);
                fillLightGo.transform.localRotation = Quaternion.Euler(-20f, -140f, 0f);

                // 5. Initial Camera Positioning
                Vector3 center = bounds.center;
                float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (maxDim <= 0.001f) maxDim = 0.2f;

                float distance = (maxDim * 0.6f) / Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);
                float minSafeDistance = (maxDim / 2f) + cam.nearClipPlane + 0.02f;
                distance = Mathf.Max(distance, minSafeDistance);

                Vector3 viewDir = new Vector3(0.2f, 0.12f, 1f).normalized;
                camGo.transform.position = center + viewDir * distance;
                camGo.transform.LookAt(center);

                Physics.SyncTransforms();

                // Pass 1: Initial Render
                cam.Render();

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
                texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                texture.Apply();

                // Pass 2: Auto-Framing & Zoom to 85% content fill
                Rect contentRect = CalculateVisiblePixelsRect(texture);
                if (contentRect.width > 0 && contentRect.height > 0)
                {
                    Vector2 currentCenter = contentRect.center;
                    Vector2 centerOffset = currentCenter - new Vector2(0.5f, 0.5f);
                    float contentMaxDim = Mathf.Max(contentRect.width, contentRect.height);
                    float targetFill = 0.82f;

                    if (contentMaxDim < targetFill * 0.8f || Mathf.Abs(centerOffset.x) > 0.08f || Mathf.Abs(centerOffset.y) > 0.08f)
                    {
                        float visibleHeightAtDist = 2.0f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                        float visibleWidthAtDist = visibleHeightAtDist * cam.aspect;

                        Vector3 moveOffset = cam.transform.right * (centerOffset.x * visibleWidthAtDist) +
                                             cam.transform.up * (centerOffset.y * visibleHeightAtDist);
                        camGo.transform.position += moveOffset;

                        float zoomFactor = contentMaxDim / targetFill;
                        zoomFactor = Mathf.Max(zoomFactor, 0.1f);

                        float newDistance = distance * zoomFactor;
                        newDistance = Mathf.Max(newDistance, 0.1f);

                        camGo.transform.position += cam.transform.forward * (distance - newDistance);
                        cam.Render();

                        texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                        texture.Apply();
                    }
                }

                RenderTexture.active = prevActive;
                return texture;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ND ThumbnailGenerator] Failed to capture thumbnail for {target.name}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
            finally
            {
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (clone != null) Object.DestroyImmediate(clone);
            }
        }

        private static void RecursiveSetLayer(GameObject obj, int layerIndex)
        {
            if (obj == null) return;
            obj.layer = layerIndex;
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                RecursiveSetLayer(obj.transform.GetChild(i).gameObject, layerIndex);
            }
        }

        private static Rect CalculateVisiblePixelsRect(Texture2D tex)
        {
            int w = tex.width;
            int h = tex.height;
            Color[] pixels = tex.GetPixels();
            int minX = w, maxX = 0, minY = h, maxY = 0;
            bool found = false;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a > 0.02f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        found = true;
                    }
                }
            }

            if (!found) return new Rect(0, 0, 0, 0);

            float xNorm = (float)minX / w;
            float yNorm = (float)minY / h;
            float wNorm = (float)(maxX - minX + 1) / w;
            float hNorm = (float)(maxY - minY + 1) / h;
            return new Rect(xNorm, yNorm, wNorm, hNorm);
        }

        private static bool EnsureThumbnailLayer()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0 || assets[0] == null) return false;

            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            if (layers == null) return false;

            // Check if already exists
            for (int i = 0; i < MaxLayers; i++)
            {
                var elem = layers.GetArrayElementAtIndex(i);
                if (elem != null && elem.stringValue == ThumbnailLayerName)
                {
                    return true;
                }
            }

            // Allocate free layer
            for (int i = MaxLayers - 1; i >= StartingUserLayer; i--)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (layer != null && string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = ThumbnailLayerName;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return true;
                }
            }

            return false;
        }

        public static Texture2D SaveThumbnailAsset(Texture2D texture, string avatarName, string itemName)
        {
            if (texture == null) return null;

            // Save into Assets/NDMenuManager/Thumbnails for valid Unity AssetDatabase path resolution
            string relFolder = "Assets/NDMenuManager/Thumbnails";
            string diskFolder = Path.GetFullPath(relFolder);

            if (!Directory.Exists(diskFolder))
            {
                Directory.CreateDirectory(diskFolder);
            }

            string safeName = itemName.Replace(" ", "_").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
            string diskFilePath = Path.Combine(diskFolder, $"Icon_{safeName}.png");
            string assetPath = $"{relFolder}/Icon_{safeName}.png";

            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(diskFilePath, pngData);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 256;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
