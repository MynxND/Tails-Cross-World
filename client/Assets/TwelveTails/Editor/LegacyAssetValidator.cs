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
                var prefabPath = $"Assets/TwelveTails/LegacyPrivate/Resources/OriginalCharacters/{expectedName}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    var prefabRenderers = prefab.GetComponentsInChildren<Renderer>(true);
                    var prefabMaterials = prefabRenderers.SelectMany(item => item.sharedMaterials).ToArray();
                    var valid = prefabMaterials.Length > 0 && prefabMaterials.All(material => material != null &&
                        material.shader != null && material.shader.isSupported && MaterialTexture(material) != null);
                    report.Add($"  PREFAB {(valid ? "PASS" : "FAIL")}: renderers={prefabRenderers.Length}, " +
                               $"materials={prefabMaterials.Length}, textured={prefabMaterials.Count(item => item != null && MaterialTexture(item) != null)}");
                    if (!valid) report.Add($"FAIL {expectedName}: generated prefab material is invalid");
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
            const string generated = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialDestination = generated + "/Materials";
            const string textureDestination = generated + "/Textures";
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resources, "OriginalCharacters");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generated, "Materials");
            EnsureFolder(generated, "Textures");

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
                ApplyDefaultAppearance(instance, title, materialDestination, textureDestination);
                AttachDefaultAccessory(instance, title);
                ConvertMaterials(instance, title, materialDestination);
                PrefabUtility.SaveAsPrefabAsset(instance, $"{destination}/{title}.prefab");
                Object.DestroyImmediate(instance);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void AttachDefaultAccessory(GameObject characterRoot, string character)
        {
            if (character != "Cat") return;
            const string path = "Assets/TwelveTails/LegacyPrivate/Resources/gameassets/characters/heroes/cat/accessories/default.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new System.Exception("Cat default hair accessory is missing");
            var head = characterRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "Head");
            if (head == null) throw new System.Exception("Cat Head bone is missing");
            var accessory = Object.Instantiate(source, head);
            accessory.name = "Default Hair";
            accessory.transform.localPosition = Vector3.zero;
            accessory.transform.localRotation = Quaternion.Euler(0f, 270f, 90f);
            accessory.transform.localScale = Vector3.one;
            accessory.SetActive(true);
            RemoveMissingScripts(accessory);
        }

        private static void ApplyDefaultAppearance(GameObject root, string character, string materials, string textures)
        {
            var renderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
            if (renderer == null) return;
            var lower = character.ToLowerInvariant();
            var armorRoot = $"Assets/TwelveTails/LegacyPrivate/Resources/gameassets/characters/heroes/{lower}/armors";
            var armorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{armorRoot}/{character}_standard.prefab");
            var armorRenderer = armorPrefab != null ? armorPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (armorRenderer != null && armorRenderer.sharedMesh != null) renderer.sharedMesh = armorRenderer.sharedMesh;

            var baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{armorRoot}/materials/{character}_standard1.png");
            var overlay = AssetDatabase.LoadAssetAtPath<Texture2D>($"{armorRoot}/overlay/{character}0.png");
            if (baseTexture == null || overlay == null)
                throw new System.Exception($"Default textures are missing for {character}");
            MakeReadable(baseTexture);
            MakeReadable(overlay);

            var texturePath = $"{textures}/{character}_standard_skin100.png";
            var pixels = baseTexture.GetPixels();
            var overlayPixels = overlay.GetPixels();
            for (var y = 256; y < 512; y++)
            for (var x = 0; x < 256; x++)
            {
                var index = y * 512 + x;
                var overlayIndex = (y - 256) * 256 + x;
                var alpha = overlayPixels[overlayIndex].a;
                pixels[index] = alpha * overlayPixels[overlayIndex] + (1f - alpha) * pixels[index];
            }
            var combined = new Texture2D(512, 512, TextureFormat.RGB24, true);
            combined.SetPixels(pixels);
            combined.Apply();
            File.WriteAllBytes(texturePath, combined.EncodeToPNG());
            Object.DestroyImmediate(combined);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            var materialPath = $"{materials}/{character}_DefaultAppearance.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", importedTexture);
            material.SetColor("_BaseColor", new Color(.86f, .86f, .86f, 1f));
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            renderer.sharedMaterial = material;
        }

        private static void MakeReadable(Texture2D texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.isReadable) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static void ConvertMaterials(GameObject root, string character, string destination)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var converted = renderer.sharedMaterials.Select(source =>
                    ConvertMaterial(source, character, destination)).ToArray();
                renderer.sharedMaterials = converted;
            }
        }

        private static Material ConvertMaterial(Material source, string character, string destination)
        {
            if (source == null) return null;
            if (source.shader != null && source.shader.isSupported &&
                source.shader.name.StartsWith("Universal Render Pipeline/")) return source;
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var guid = AssetDatabase.AssetPathToGUID(sourcePath);
            var suffix = guid.Length >= 8 ? guid.Substring(0, 8) : "material";
            var safeName = string.Concat(source.name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            var path = $"{destination}/{character}_{safeName}_{suffix}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new System.Exception("URP Lit shader is unavailable");
                material = new Material(shader) { name = $"{character}_{source.name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            var texture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : source.mainTexture;
            var color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            if (source.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
            }

            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Cull", 0f);
            ConfigureSurface(material, source.shader != null ? source.shader.name : string.Empty, source.renderQueue);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture MaterialTexture(Material material)
        {
            if (material.HasProperty("_BaseMap")) return material.GetTexture("_BaseMap");
            if (material.HasProperty("_MainTex")) return material.GetTexture("_MainTex");
            return material.mainTexture;
        }

        private static void ConfigureSurface(Material material, string legacyShader, int legacyQueue)
        {
            var shader = legacyShader.ToLowerInvariant();
            var transparent = legacyQueue >= 3000 || shader.Contains("transparent") ||
                              shader.Contains("additive") || shader.Contains("alpha");
            var cutout = shader.Contains("cutout");
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", .5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = 2450;
                return;
            }

            material.SetFloat("_AlphaClip", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            material.SetFloat("_SrcBlend", transparent ? 5f : 1f);
            material.SetFloat("_DstBlend", transparent ? 10f : 0f);
            if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = transparent ? 3000 : 2000;
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

        [MenuItem("12 Tails/Generate Original M101 Map Pilot")]
        public static void GenerateOriginalMapPilot()
        {
            const string sourcePath = "Assets/TwelveTails/LegacyPrivate/Scene/M101_CarronHarvest.unity";
            const string generatedRoot = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialRoot = generatedRoot + "/MapMaterials";
            const string sceneRoot = "Assets/TwelveTails/LegacyPrivate/Scenes";
            const string outputPath = sceneRoot + "/M101_CarronHarvest_URP.unity";
            const string resourceRoot = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string mapResourceRoot = resourceRoot + "/OriginalMaps";
            const string monsterResourceRoot = resourceRoot + "/OriginalMonsters";
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Original M101 scene is missing", sourcePath);
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generatedRoot, "MapMaterials");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Scenes");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resourceRoot, "OriginalMaps");
            EnsureFolder(resourceRoot, "OriginalMonsters");

            var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                RemoveMissingScripts(root);
                ConvertMaterials(root, "M101", materialRoot);
            }

            var carronCandidates = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == "Carron")
                .Select(item => new { Transform = item, Renderers = item.GetComponentsInChildren<Renderer>(true) })
                .Where(item => item.Renderers.Length > 0)
                .OrderBy(item => item.Renderers.Length)
                .ToArray();
            if (carronCandidates.Length == 0)
                throw new System.Exception("No rendered Carron root was found in M101");

            var carronInstance = Object.Instantiate(carronCandidates[0].Transform.gameObject);
            carronInstance.name = "Carron";
            carronInstance.SetActive(true);
            carronInstance.transform.SetParent(null, false);
            carronInstance.transform.localPosition = Vector3.zero;
            carronInstance.transform.localRotation = Quaternion.identity;
            RemoveMissingScripts(carronInstance);
            foreach (var collider in carronInstance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            PrefabUtility.SaveAsPrefabAsset(carronInstance, monsterResourceRoot + "/Carron.prefab");
            Object.DestroyImmediate(carronInstance);

            EditorSceneManager.SaveScene(scene, outputPath, true);
            var sceneObjects = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "SceneObjects");
            if (sceneObjects == null) throw new System.Exception("M101 SceneObjects root is missing");
            var mapInstance = Object.Instantiate(sceneObjects);
            mapInstance.name = "M101_CarronHarvest";
            var embeddedCarrons = mapInstance.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "Carron")
                .OrderByDescending(item => HierarchyDepth(item))
                .ToArray();
            foreach (var embeddedCarron in embeddedCarrons)
                Object.DestroyImmediate(embeddedCarron.gameObject);
            PrefabUtility.SaveAsPrefabAsset(mapInstance, mapResourceRoot + "/M101_CarronHarvest.prefab");
            Object.DestroyImmediate(mapInstance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateOriginalMapPilot();
        }

        [MenuItem("12 Tails/Validate Original M101 Map Pilot")]
        public static void ValidateOriginalMapPilot()
        {
            const string scenePath = "Assets/TwelveTails/LegacyPrivate/Scenes/M101_CarronHarvest_URP.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var renderers = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
            var meshFilters = roots.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            var skinned = roots.SelectMany(root => root.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToArray();
            var meshes = meshFilters.Select(item => item.sharedMesh)
                .Concat(skinned.Select(item => item.sharedMesh)).Where(item => item != null).Distinct().ToArray();
            var materials = renderers.SelectMany(item => item.sharedMaterials)
                .Where(item => item != null).Distinct().ToArray();
            var colliders = roots.SelectMany(root => root.GetComponentsInChildren<Collider>(true)).ToArray();
            var terrains = roots.SelectMany(root => root.GetComponentsInChildren<Terrain>(true)).ToArray();
            var sourceCarrons = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Count(item => item.name == "Carron");
            var environment = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TwelveTails/LegacyPrivate/Resources/OriginalMaps/M101_CarronHarvest.prefab");
            var environmentCarrons = environment == null ? -1 : environment.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name == "Carron");
            var carronPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TwelveTails/LegacyPrivate/Resources/OriginalMonsters/Carron.prefab");
            var carronRenderers = carronPrefab == null
                ? System.Array.Empty<Renderer>()
                : carronPrefab.GetComponentsInChildren<Renderer>(true);
            var carronMaterials = carronRenderers.SelectMany(item => item.sharedMaterials)
                .Where(item => item != null).Distinct().ToArray();
            var carronBadMaterials = carronMaterials.Count(item => item.shader == null || !item.shader.isSupported ||
                item.shader.name == "Hidden/InternalErrorShader");
            var carronAnimation = carronPrefab == null ? null : carronPrefab.GetComponentInChildren<Animation>(true);
            var carronAnimations = carronAnimation == null ? 0 : 1;
            var carronAnimationClips = carronAnimation == null ? 0 : carronAnimation.GetClipCount();
            var carronDefaultClip = carronAnimation != null && carronAnimation.clip != null
                ? carronAnimation.clip.name
                : "none";
            var carronAnimators = carronPrefab == null ? 0 : carronPrefab.GetComponentsInChildren<Animator>(true).Length;
            var badMaterials = materials.Count(item => item.shader == null || !item.shader.isSupported ||
                item.shader.name == "Hidden/InternalErrorShader");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var report = new[]
            {
                "Twelve Tails original M101 map pilot validation",
                $"Unity: {Application.unityVersion}",
                $"roots={roots.Length}",
                $"renderers={renderers.Length}",
                $"meshes={meshes.Length}",
                $"materials={materials.Length}",
                $"colliders={colliders.Length}",
                $"terrains={terrains.Length}",
                $"sourceCarrons={sourceCarrons}",
                $"environmentCarrons={environmentCarrons}",
                $"carronPrefabRenderers={carronRenderers.Length}",
                $"carronPrefabMaterials={carronMaterials.Length}",
                $"carronPrefabUnsupportedMaterials={carronBadMaterials}",
                $"carronPrefabAnimations={carronAnimations}",
                $"carronPrefabAnimationClips={carronAnimationClips}",
                $"carronPrefabDefaultClip={carronDefaultClip}",
                $"carronPrefabAnimators={carronAnimators}",
                $"unsupportedMaterials={badMaterials}",
                $"rootNames={string.Join(",", roots.Select(item => item.name))}",
                $"boundsCenter={bounds.center}",
                $"boundsSize={bounds.size}",
                $"terrainPosition={(terrains.Length > 0 ? terrains[0].transform.position.ToString() : "none")}",
                $"terrainSize={(terrains.Length > 0 ? terrains[0].terrainData.size.ToString() : "none")}"
            };
            var repositoryRoot = Directory.GetParent(Application.dataPath)?.Parent?.FullName ?? ".";
            var artifactDirectory = Path.Combine(repositoryRoot, "artifacts");
            Directory.CreateDirectory(artifactDirectory);
            var reportPath = Path.Combine(artifactDirectory, "legacy-m101-validation.txt");
            File.WriteAllLines(reportPath, report);
            Debug.Log(string.Join("\n", report));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (renderers.Length == 0 || meshes.Length == 0 || colliders.Length == 0 || terrains.Length == 0 ||
                sourceCarrons == 0 || environmentCarrons != 0 || carronRenderers.Length == 0 ||
                carronMaterials.Length == 0 || carronBadMaterials > 0 || carronAnimationClips == 0 ||
                badMaterials > 0)
                throw new System.Exception($"Original M101 map validation failed. See {reportPath}");
        }

        private static int HierarchyDepth(Transform transform)
        {
            var depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }
            return depth;
        }
    }
}
