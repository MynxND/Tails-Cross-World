using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwelveTails.EditorTools
{
    public static class LegacyAssetValidator
    {
        private const string SceneFolder = "Assets/TwelveTails/LegacyPrivate/TTOAssetHolder/Scenes";

        private static readonly string[] CharacterScenes =
        {
            "C01_WolfAsset.unity", "C02_BisonAsset.unity", "C03_PandaAsset.unity",
            "C04_WhaleAsset.unity", "C05_CatAsset.unity", "C06_ChameleonAsset.unity",
            "C07_RabbitAssets.unity", "C08_MoleAsset.unity", "C09_MonkeyAsset.unity",
            "C10_PenguinAsset.unity", "C11_SheepAsset.unity", "C12_BatAsset.unity"
        };

        [MenuItem("12 Tails/Validate Original Character Assets")]
        public static void ValidateOriginalCharacterAssets()
        {
            var report = new List<string>
            {
                "Twelve Tails original character asset validation",
                $"Unity: {Application.unityVersion}"
            };

            foreach (var filename in CharacterScenes)
            {
                var path = $"{SceneFolder}/{filename}";
                if (!File.Exists(path))
                {
                    report.Add($"FAIL {filename}: scene is missing");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var renderers = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
                var meshFilters = roots.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)).ToArray();
                var skinned = roots.SelectMany(root => root.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToArray();
                var meshes = meshFilters.Select(item => item.sharedMesh)
                    .Concat(skinned.Select(item => item.sharedMesh)).Where(mesh => mesh != null).Distinct().ToArray();
                var materials = renderers.SelectMany(item => item.sharedMaterials)
                    .Where(material => material != null).Distinct().ToArray();
                var missingShaders = materials.Count(material => material.shader == null ||
                    material.shader.name == "Hidden/InternalErrorShader");
                var status = meshes.Length > 0 && materials.Length > 0 ? "PASS" : "FAIL";
                report.Add($"{status} {filename}: roots={roots.Length}, renderers={renderers.Length}, " +
                           $"meshes={meshes.Length}, materials={materials.Length}, missingShaders={missingShaders}");
                var expectedName = filename.Split('_')[1].Replace("Assets.unity", "").Replace("Asset.unity", "");
                var candidates = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(item => item.name.Equals(expectedName, System.StringComparison.OrdinalIgnoreCase)).ToArray();
                foreach (var candidate in candidates)
                {
                    var candidateRenderers = candidate.GetComponentsInChildren<Renderer>(true);
                    report.Add($"  candidate={candidate.name}, active={candidate.gameObject.activeSelf}, " +
                               $"children={candidate.childCount}, renderers={candidateRenderers.Length}");
                }
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var repositoryRoot = Directory.GetParent(Application.dataPath)?.Parent?.FullName ?? ".";
            var artifactDirectory = Path.Combine(repositoryRoot, "artifacts");
            Directory.CreateDirectory(artifactDirectory);
            var reportPath = Path.Combine(artifactDirectory, "legacy-character-validation.txt");
            File.WriteAllLines(reportPath, report);
            Debug.Log(string.Join("\n", report));

            if (report.Any(line => line.StartsWith("FAIL")))
                throw new System.Exception($"Legacy character validation failed. See {reportPath}");
        }

        [MenuItem("12 Tails/Generate Original Character Prefabs")]
        public static void GenerateOriginalCharacterPrefabsIfAvailable()
        {
            if (!AssetDatabase.IsValidFolder("Assets/TwelveTails/LegacyPrivate")) return;
            const string resources = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string destination = resources + "/OriginalCharacters";
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resources, "OriginalCharacters");

            foreach (var filename in CharacterScenes)
            {
                var path = $"{SceneFolder}/{filename}";
                if (!File.Exists(path)) continue;
                var title = filename.Split('_')[1].Replace("Assets.unity", "").Replace("Asset.unity", "");
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var candidates = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(item => item.name.Equals(title, System.StringComparison.OrdinalIgnoreCase))
                    .Select(item => new { Transform = item, Renderers = item.GetComponentsInChildren<Renderer>(true) })
                    .Where(item => item.Renderers.Length > 0)
                    .OrderBy(item => item.Renderers.Length)
                    .ToArray();
                if (candidates.Length == 0) throw new System.Exception($"No model root found for {title}");

                var instance = Object.Instantiate(candidates[0].Transform.gameObject);
                instance.name = title;
                instance.SetActive(true);
                instance.transform.SetParent(null, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                RemoveMissingScripts(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, $"{destination}/{title}.prefab");
                Object.DestroyImmediate(instance);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RemoveMissingScripts(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var fullPath = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(fullPath)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
