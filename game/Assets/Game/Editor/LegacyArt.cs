using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TuringSignalRelay.Editor
{
    public static class LegacyArt
    {
        public static void Prepare()
        {
            if (!Directory.Exists("Assets/Game/Art/LegacyModels"))
                throw new DirectoryNotFoundException("Add the locally authorized legacy FBX models before baking art.");
            string destination = "Assets/Game/Resources/LegacySprites";
            Directory.CreateDirectory(destination);
            Directory.CreateDirectory("Assets/Game/Art/LegacyMaterials");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("BakeCamera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 1000;
            var target = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Game/Art/LegacyModels" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (original == null)
                    throw new Exception("Model import failed: " + path);
                var instance = UnityEngine.Object.Instantiate(original);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    throw new Exception("No renderers: " + path);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    renderer.enabled = true;
                    Debug.Log("BAKE_PART " + path + " " + renderer.name + " " + renderer.bounds);
                    bounds.Encapsulate(renderer.bounds);
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        string name = materials[i] == null ? "Default" : materials[i].name;
                        string safeName = name.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                        string materialPath = "Assets/Game/Art/LegacyMaterials/" + safeName + ".mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (material == null)
                        {
                            material = new Material(Shader.Find("Unlit/Color"));
                            int hash = 0;
                            foreach (char letter in name)
                                hash = unchecked(hash * 31 + letter);
                            material.color = Color.HSVToRGB((hash & 255) / 255f, .3f, .8f);
                            AssetDatabase.CreateAsset(material, materialPath);
                        }
                        materials[i] = material;
                        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
                    }
                    renderer.sharedMaterials = materials;
                }
                float radius = Math.Max(.01f, bounds.extents.magnitude);
                bool character = path.Contains("police") || path.Contains("crime") || path.Contains("student");
                Vector3 direction = character ? new Vector3(0, 1, -.65f).normalized : Vector3.up;
                camera.transform.position = bounds.center + direction * radius * 4;
                camera.transform.rotation = Quaternion.LookRotation(-direction, character ? Vector3.up : Vector3.forward);
                camera.orthographicSize = radius * 1.12f;
                camera.nearClipPlane = radius * .01f;
                camera.farClipPlane = radius * 10;
                Debug.Log("BAKE_MODEL " + path + " bounds=" + bounds + " radius=" + radius);
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                int opaque = 0;
                foreach (var pixel in pixels)
                if (pixel.a > 0)
                    opaque++;
                if (opaque < 50)
                    throw new Exception("Empty baked sprite: " + path);
                string output = destination + "/" + Path.GetFileNameWithoutExtension(path) + ".png";
                int minX = 1024, minY = 1024, maxX = 0, maxY = 0;
                for (int y = 0; y < 1024; y++)
                for (int x = 0; x < 1024; x++)
                if (pixels[y * 1024 + x].a > 0)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
                float scale = 224f / Math.Max(maxX - minX + 1, maxY - minY + 1);
                var cropped = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                var result = new Color32[256 * 256];
                int width = Math.Max(1, (int)((maxX - minX + 1) * scale)), height = Math.Max(1, (int)((maxY - minY + 1) * scale));
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    result[(y + (256 - height) / 2) * 256 + x + (256 - width) / 2] = pixels[(minY + Math.Min(maxY - minY, (int)(y / scale))) * 1024 + minX + Math.Min(maxX - minX, (int)(x / scale))];
                cropped.SetPixels32(result);
                cropped.Apply();
                File.WriteAllBytes(output, cropped.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(cropped);
                AssetDatabase.WriteImportSettingsIfDirty(path);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(instance);
                count++;
            }
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { destination }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 256;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            var observer = new GameObject("ObserverRoot");
            observer.AddComponent<ObserverController>();
            Directory.CreateDirectory("Assets/Game/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(observer, "Assets/Game/Prefabs/ObserverRoot.prefab");
            EditorSceneManager.SaveScene(scene, "Assets/Game/Scenes/Observer.unity");
            var bootstrap = EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var clock = UnityEngine.Object.FindFirstObjectByType<BootstrapController>();
            if (clock != null)
                clock.enabled = false;
            if (UnityEngine.Object.FindFirstObjectByType<ObserverController>() == null)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/ObserverRoot.prefab"));
            EditorSceneManager.SaveScene(bootstrap);
            Debug.Log("LEGACY_ART_PREPARED count=" + count);
        }
    }
}
