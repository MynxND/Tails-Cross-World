using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TwelveTails.Gameplay;

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
                AttachOriginalAnimationClips(instance, title);
                ConvertMaterials(instance, title, materialDestination);
                PrefabUtility.SaveAsPrefabAsset(instance, $"{destination}/{title}.prefab");
                Object.DestroyImmediate(instance);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void AttachOriginalAnimationClips(GameObject characterRoot, string character)
        {
            var animation = characterRoot.GetComponentInChildren<Animation>(true);
            if (animation == null) throw new System.Exception($"Original {character} prefab has no Animation component");
            var rigRoot = $"{character}_tri";
            var basicAttack = character == "Rabbit" ? "nAttack" : "nAttack1";
            var clipNames = new[] { basicAttack, "nAttack2", "cAttack1", "hit", "ko", "getUp" }.ToList();
            if (character == "Mole") clipNames.Add("grenade");
            if (character == "Wolf") clipNames.AddRange(new[] { "bladeFang1", "bladeFang2", "bladeFang3" });
            foreach (var clipName in clipNames)
            {
                var clip = AssetDatabase.FindAssets($"{clipName} t:AnimationClip", new[] { "Assets/TwelveTails/LegacyPrivate/AnimationClip" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                    .FirstOrDefault(candidate => candidate != null && candidate.name == clipName &&
                        AnimationUtility.GetCurveBindings(candidate).Any(binding =>
                            binding.path.Equals(rigRoot, System.StringComparison.OrdinalIgnoreCase) ||
                            binding.path.StartsWith(rigRoot + "/", System.StringComparison.OrdinalIgnoreCase)));
                if (clip == null)
                {
                    Debug.LogWarning($"Original {character} clip {clipName} was not found for rig {rigRoot}; runtime fallback remains enabled.");
                    continue;
                }
                animation.RemoveClip(clipName);
                animation.AddClip(clip, clipName);
            }
            if (characterRoot.GetComponentInChildren<LegacyAnimationDriver>(true) == null)
                animation.gameObject.AddComponent<LegacyAnimationDriver>();
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
            RepairM101StaticBatchedStructures(scene.GetRootGameObjects());

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

        [MenuItem("12 Tails/Generate Original Mupo Prefab")]
        public static void GenerateOriginalMupoPrefab()
        {
            const string sourcePath = "Assets/TwelveTails/LegacyPrivate/Scene/M102_MupoRoundUp.unity";
            const string generatedRoot = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialRoot = generatedRoot + "/MonsterMaterials";
            const string resourceRoot = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string monsterResourceRoot = resourceRoot + "/OriginalMonsters";
            const string outputPath = monsterResourceRoot + "/Mupo.prefab";
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Original M102 scene is missing", sourcePath);

            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generatedRoot, "MonsterMaterials");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resourceRoot, "OriginalMonsters");

            var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            var candidates = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == "Mupo_r" || item.name == "Mupo_g")
                .Select(item => new { Transform = item, Renderers = item.GetComponentsInChildren<Renderer>(true) })
                .Where(item => item.Renderers.Length > 0)
                .OrderBy(item => item.Renderers.Length)
                .ToArray();
            if (candidates.Length == 0) throw new System.Exception("No rendered Mupo root was found in M102");

            var instance = Object.Instantiate(candidates[0].Transform.gameObject);
            instance.name = "Mupo";
            instance.SetActive(true);
            instance.transform.SetParent(null, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            RemoveMissingScripts(instance);
            ConvertMaterials(instance, "Mupo", materialRoot);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            var renderers = prefab == null ? System.Array.Empty<Renderer>() : prefab.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(item => item.sharedMaterials).Where(item => item != null).Distinct().ToArray();
            if (prefab == null || renderers.Length == 0 || materials.Length == 0)
                throw new System.Exception("Generated Mupo prefab has no usable visual assets");
            Debug.Log($"Generated {outputPath}: renderers={renderers.Length}, materials={materials.Length}");
        }

        [MenuItem("12 Tails/Generate Original StingBug Prefab")]
        public static void GenerateOriginalStingBugPrefab()
        {
            const string sourcePath = "Assets/TwelveTails/LegacyPrivate/Scene/M103_BugTrouble.unity";
            const string generatedRoot = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialRoot = generatedRoot + "/MonsterMaterials";
            const string resourceRoot = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string monsterResourceRoot = resourceRoot + "/OriginalMonsters";
            const string outputPath = monsterResourceRoot + "/StingBug.prefab";
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Original M103 scene is missing", sourcePath);

            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generatedRoot, "MonsterMaterials");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resourceRoot, "OriginalMonsters");

            var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            var candidates = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == "StingBug_green")
                .Select(item => new { Transform = item, Renderers = item.GetComponentsInChildren<Renderer>(true) })
                .Where(item => item.Renderers.Length > 0)
                .OrderBy(item => item.Renderers.Length)
                .ToArray();
            if (candidates.Length == 0) throw new System.Exception("No rendered StingBug root was found in M103");

            var instance = Object.Instantiate(candidates[0].Transform.gameObject);
            instance.name = "StingBug";
            instance.SetActive(true);
            instance.transform.SetParent(null, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            RemoveMissingScripts(instance);
            ConvertMaterials(instance, "StingBug", materialRoot);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            var renderers = prefab == null ? System.Array.Empty<Renderer>() : prefab.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(item => item.sharedMaterials).Where(item => item != null).Distinct().ToArray();
            if (prefab == null || renderers.Length == 0 || materials.Length == 0)
                throw new System.Exception("Generated StingBug prefab has no usable visual assets");
            Debug.Log($"Generated {outputPath}: renderers={renderers.Length}, materials={materials.Length}");
        }

        [MenuItem("12 Tails/Generate Original M103 Environment")]
        public static void GenerateOriginalM103Environment()
        {
            const string sourcePath = "Assets/TwelveTails/LegacyPrivate/Scene/M103_BugTrouble.unity";
            const string generatedRoot = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialRoot = generatedRoot + "/M103Materials";
            const string resourceRoot = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string mapResourceRoot = resourceRoot + "/OriginalChapter1Maps";
            const string npcResourceRoot = resourceRoot + "/OriginalNpcs";
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Original M103 scene is missing", sourcePath);

            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generatedRoot, "M103Materials");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resourceRoot, "OriginalChapter1Maps");
            EnsureFolder(resourceRoot, "OriginalNpcs");

            var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            var sceneObjects = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "SceneObjects");
            if (sceneObjects == null) throw new System.Exception("M103 SceneObjects root is missing");

            var goat = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "GoatFarmer");
            if (goat == null) throw new System.Exception("GoatFarmer was not found in M103");
            var goatInstance = Object.Instantiate(goat.gameObject);
            goatInstance.name = "GoatFarmer";
            goatInstance.SetActive(true);
            goatInstance.transform.SetParent(null, false);
            goatInstance.transform.localPosition = Vector3.zero;
            goatInstance.transform.localRotation = Quaternion.identity;
            RemoveMissingScripts(goatInstance);
            ConvertMaterials(goatInstance, "M103_GoatFarmer", materialRoot);
            foreach (var collider in goatInstance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            var goatOutputPath = npcResourceRoot + "/GoatFarmer.prefab";
            PrefabUtility.SaveAsPrefabAsset(goatInstance, goatOutputPath);
            Object.DestroyImmediate(goatInstance);

            var mapInstance = Object.Instantiate(sceneObjects);
            mapInstance.name = "M103_BugTrouble";
            RemoveMissingScripts(mapInstance);
            ConvertMaterials(mapInstance, "M103_Map", materialRoot);
            var dynamicNames = new[] { "StingBug_green", "GoatFarmer", "Carron" };
            var dynamicActors = mapInstance.GetComponentsInChildren<Transform>(true)
                .Where(item => dynamicNames.Contains(item.name))
                .OrderByDescending(HierarchyDepth)
                .ToArray();
            foreach (var actor in dynamicActors) Object.DestroyImmediate(actor.gameObject);
            PrefabUtility.SaveAsPrefabAsset(mapInstance, mapResourceRoot + "/M103_BugTrouble.prefab");
            Object.DestroyImmediate(mapInstance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var goatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(goatOutputPath);
            if (goatPrefab == null || goatPrefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new System.Exception("Generated GoatFarmer prefab has no usable renderers");
            Debug.Log("Generated original M103 environment and GoatFarmer prefabs.");
        }

        [MenuItem("12 Tails/Generate Original M104 Actors")]
        public static void GenerateOriginalM104Actors()
        {
            const string sourcePath = "Assets/TwelveTails/LegacyPrivate/Scene/M104_StingbugNest.unity";
            const string generatedRoot = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialRoot = generatedRoot + "/MonsterMaterials";
            const string resourceRoot = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string monsterResourceRoot = resourceRoot + "/OriginalMonsters";
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Original M104 scene is missing", sourcePath);

            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generatedRoot, "MonsterMaterials");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resourceRoot, "OriginalMonsters");

            var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            ExtractRenderedPrefab(scene, "StingNest", monsterResourceRoot + "/StingNest.prefab", "StingNest", materialRoot);
            ExtractRenderedPrefab(scene, "StingBug_red", monsterResourceRoot + "/StingBugRed.prefab", "StingBugRed", materialRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated original M104 StingNest and StingBugRed prefabs.");
        }

        [MenuItem("12 Tails/Generate Original M105 Actors")]
        public static void GenerateOriginalM105Actors()
        {
            const string sourcePath = "Assets/TwelveTails/LegacyPrivate/Scene/M105_NeedleCave.unity";
            const string generatedRoot = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialRoot = generatedRoot + "/MonsterMaterials";
            const string resourceRoot = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string monsterResourceRoot = resourceRoot + "/OriginalMonsters";
            const string npcResourceRoot = resourceRoot + "/OriginalNpcs";
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Original M105 scene is missing", sourcePath);

            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generatedRoot, "MonsterMaterials");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resourceRoot, "OriginalMonsters");
            EnsureFolder(resourceRoot, "OriginalNpcs");

            var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            ExtractRenderedPrefab(scene, "MiniCat", npcResourceRoot + "/MiniCat.prefab", "MiniCat", materialRoot);
            foreach (var suffix in new[] { "g", "b", "r", "p", "o", "k" })
            {
                var sourceName = $"NeedleBug_{suffix}";
                ExtractRenderedPrefab(scene, sourceName, monsterResourceRoot + $"/NeedleBug_{suffix}.prefab", sourceName, materialRoot);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated original M105 MiniCat and six NeedleBug variant prefabs.");
        }

        [MenuItem("12 Tails/Generate Original M106 Actors")]
        public static void GenerateOriginalM106Actors()
        {
            const string sourcePath = "Assets/TwelveTails/LegacyPrivate/Scene/M106_BoldasRecruitment.unity";
            const string generatedRoot = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialRoot = generatedRoot + "/NpcMaterials";
            const string resourceRoot = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string npcResourceRoot = resourceRoot + "/OriginalNpcs";
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Original M106 scene is missing", sourcePath);

            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generatedRoot, "NpcMaterials");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resourceRoot, "OriginalNpcs");

            var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            ExtractRenderedPrefab(scene, "Liger_mallet", npcResourceRoot + "/Boldas.prefab", "Boldas", materialRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated original M106 Boldas prefab.");
        }

        [MenuItem("12 Tails/Generate Original M107 Actors")]
        public static void GenerateOriginalM107Actors()
        {
            const string sourcePath = "Assets/TwelveTails/LegacyPrivate/Scene/M107_RequestFromAlcacia.unity";
            const string generatedRoot = "Assets/TwelveTails/LegacyPrivate/Generated";
            const string materialRoot = generatedRoot + "/NpcMaterials";
            const string resourceRoot = "Assets/TwelveTails/LegacyPrivate/Resources";
            const string npcResourceRoot = resourceRoot + "/OriginalNpcs";
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Original M107 scene is missing", sourcePath);

            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Generated");
            EnsureFolder(generatedRoot, "NpcMaterials");
            EnsureFolder("Assets/TwelveTails/LegacyPrivate", "Resources");
            EnsureFolder(resourceRoot, "OriginalNpcs");

            var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            ExtractRenderedPrefab(scene, "LightGod", npcResourceRoot + "/LightGod.prefab", "LightGod", materialRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated original M107 LightGod prefab.");
        }

        private static void ExtractRenderedPrefab(UnityEngine.SceneManagement.Scene scene, string sourceName, string outputPath, string materialPrefix, string materialRoot)
        {
            var source = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == sourceName)
                .FirstOrDefault(item => item.GetComponentsInChildren<Renderer>(true).Length > 0);
            if (source == null) throw new System.Exception($"Rendered {sourceName} was not found");

            var instance = Object.Instantiate(source.gameObject);
            instance.name = sourceName;
            instance.SetActive(true);
            instance.transform.SetParent(null, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            RemoveMissingScripts(instance);
            ConvertMaterials(instance, materialPrefix, materialRoot);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            Object.DestroyImmediate(instance);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            var renderers = prefab == null ? System.Array.Empty<Renderer>() : prefab.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(item => item.sharedMaterials).Where(item => item != null).Distinct().ToArray();
            if (prefab == null || renderers.Length == 0 || materials.Length == 0)
                throw new System.Exception($"Generated {sourceName} prefab has no usable visual assets");
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
            var environmentStructures = environment == null
                ? System.Array.Empty<Transform>()
                : environment.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name == "Plain_Gate" || item.name == "Plain_Fence_short" ||
                                   item.name == "Plain_Fence_long").ToArray();
            var environmentGates = environmentStructures.Count(item => item.name == "Plain_Gate");
            var environmentShortFences = environmentStructures.Count(item => item.name == "Plain_Fence_short");
            var environmentLongFences = environmentStructures.Count(item => item.name == "Plain_Fence_long");
            var invalidStructureMeshes = environmentStructures.Count(item =>
            {
                var filter = item.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) return true;
                return item.name switch
                {
                    "Plain_Gate" => filter.sharedMesh.name != "Plain_Gate_tri",
                    "Plain_Fence_short" => filter.sharedMesh.name != "PlainFence_short",
                    "Plain_Fence_long" => filter.sharedMesh.name != "PlainFence_long",
                    _ => true
                };
            });
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
                $"environmentGates={environmentGates}",
                $"environmentShortFences={environmentShortFences}",
                $"environmentLongFences={environmentLongFences}",
                $"invalidStructureMeshes={invalidStructureMeshes}",
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
                environmentGates != 3 || environmentShortFences != 14 || environmentLongFences != 6 ||
                invalidStructureMeshes > 0 || badMaterials > 0)
                throw new System.Exception($"Original M101 map validation failed. See {reportPath}");
        }

        private static void RepairM101StaticBatchedStructures(IEnumerable<GameObject> roots)
        {
            var meshes = new Dictionary<string, Mesh>
            {
                ["Plain_Gate"] = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/TwelveTails/LegacyPrivate/Mesh/Plain_Gate_tri.asset"),
                ["Plain_Fence_short"] = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/TwelveTails/LegacyPrivate/Mesh/PlainFence_short.asset"),
                ["Plain_Fence_long"] = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/TwelveTails/LegacyPrivate/Mesh/PlainFence_long.asset")
            };
            if (meshes.Values.Any(mesh => mesh == null))
                throw new System.Exception("M101 individual gate/fence meshes are missing");

            foreach (var transform in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            {
                if (!meshes.TryGetValue(transform.name, out var mesh)) continue;
                var filter = transform.GetComponent<MeshFilter>();
                if (filter == null) throw new System.Exception($"{transform.name} is missing its MeshFilter");
                filter.sharedMesh = mesh;
            }
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

        [MenuItem("12 Tails/Validate Imported Chapter 1 Scenes")]
        public static void ValidateImportedChapterOneScenes()
        {
            const string folder = "Assets/TwelveTails/LegacyPrivate/Scene";
            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path).StartsWith("M1", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path)
                .ToArray();
            var report = new List<string>
            {
                "Twelve Tails imported Chapter 1 scene validation",
                $"Unity: {Application.unityVersion}",
                $"scenes={scenePaths.Length}"
            };
            var failures = 0;
            foreach (var path in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var filters = roots.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)).ToArray();
                var skinned = roots.SelectMany(root => root.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToArray();
                var renderers = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
                var materials = renderers.SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null).Distinct().ToArray();
                var missingMeshComponents = filters.Where(filter => filter.sharedMesh == null).Cast<Component>()
                    .Concat(skinned.Where(renderer => renderer.sharedMesh == null)).ToArray();
                var missingMeshes = missingMeshComponents.Length;
                var expectedDynamicMeshes = missingMeshComponents.Count(component => IsExpectedDynamicMesh(component.transform));
                var unexplainedMissingMeshes = missingMeshes - expectedDynamicMeshes;
                var missingMaterialRenderers = renderers
                    .Where(renderer => renderer.sharedMaterials.Any(material => material == null)).ToArray();
                var missingMaterialSlots = missingMaterialRenderers.Sum(renderer =>
                    renderer.sharedMaterials.Count(material => material == null));
                var runtimeMaterialSlots = missingMaterialRenderers
                    .Where(renderer => IsRuntimeAssignedCharacterMaterial(renderer.transform))
                    .Sum(renderer => renderer.sharedMaterials.Count(material => material == null));
                var unexplainedMaterialSlots = missingMaterialSlots - runtimeMaterialSlots;
                var unsupportedMaterials = materials.Count(material => material.shader == null ||
                    material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported);
                var missingScripts = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
                var terrains = roots.SelectMany(root => root.GetComponentsInChildren<Terrain>(true)).Count();
                var hasExpectedRepairs = expectedDynamicMeshes > 0 || runtimeMaterialSlots > 0 ||
                                         missingScripts > 0 || unsupportedMaterials > 0;
                var status = unexplainedMissingMeshes == 0 && unexplainedMaterialSlots == 0
                    ? (hasExpectedRepairs ? "PASS_WITH_REPAIRS" : "PASS")
                    : "FAIL";
                if (status == "FAIL") failures++;
                report.Add($"{status} {Path.GetFileName(path)}: roots={roots.Length}, renderers={renderers.Length}, " +
                           $"meshes={filters.Length + skinned.Length}, missingMeshes={missingMeshes}, " +
                           $"expectedDynamicMeshes={expectedDynamicMeshes}, unexplainedMissingMeshes={unexplainedMissingMeshes}, " +
                           $"materials={materials.Length}, missingMaterialSlots={missingMaterialSlots}, " +
                           $"runtimeMaterialSlots={runtimeMaterialSlots}, unexplainedMaterialSlots={unexplainedMaterialSlots}, " +
                           $"unsupportedMaterials={unsupportedMaterials}, missingScripts={missingScripts}, terrains={terrains}");
                foreach (var filter in filters.Where(filter => filter.sharedMesh == null))
                    report.Add($"  {(IsExpectedDynamicMesh(filter.transform) ? "DYNAMIC_MESH_REBUILD" : "MISSING_MESH")} " +
                               $"{HierarchyPath(filter.transform)} (MeshFilter)");
                foreach (var renderer in skinned.Where(renderer => renderer.sharedMesh == null))
                    report.Add($"  {(IsExpectedDynamicMesh(renderer.transform) ? "DYNAMIC_MESH_REBUILD" : "MISSING_MESH")} " +
                               $"{HierarchyPath(renderer.transform)} (SkinnedMeshRenderer)");
                foreach (var renderer in missingMaterialRenderers)
                    report.Add($"  {(IsRuntimeAssignedCharacterMaterial(renderer.transform) ? "RUNTIME_CHARACTER_MATERIAL" : "MISSING_MATERIAL")} " +
                               $"{HierarchyPath(renderer.transform)} " +
                               $"slots={renderer.sharedMaterials.Count(material => material == null)}");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var repositoryRoot = Directory.GetParent(Application.dataPath)?.Parent?.FullName ?? ".";
            var artifactDirectory = Path.Combine(repositoryRoot, "artifacts");
            Directory.CreateDirectory(artifactDirectory);
            var reportPath = Path.Combine(artifactDirectory, "legacy-chapter-1-unity-validation.txt");
            File.WriteAllLines(reportPath, report);
            Debug.Log(string.Join("\n", report));
            if (scenePaths.Length != 11 || failures > 0)
                throw new System.Exception($"Imported Chapter 1 validation failed. See {reportPath}");
        }

        private static string HierarchyPath(Transform transform)
        {
            var parts = new List<string>();
            while (transform != null)
            {
                parts.Add(transform.name);
                transform = transform.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static bool IsExpectedDynamicMesh(Transform transform)
        {
            return transform.name == "LineEmitter" || transform.name == "ImageEmitter" ||
                   transform.name == "TrailEmitter" || transform.name == "ImageEffect" ||
                   transform.name == "ZodiacRing";
        }

        private static bool IsRuntimeAssignedCharacterMaterial(Transform transform)
        {
            var path = HierarchyPath(transform);
            return path.Contains("/NPC/") && transform.name.EndsWith("_tri", System.StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem("12 Tails/Generate Chapter 1 Environment Prefabs")]
        public static void GenerateChapterOneEnvironmentPrefabs()
        {
            const string sourceFolder = "Assets/TwelveTails/LegacyPrivate/Scene";
            const string privateRoot = "Assets/TwelveTails/LegacyPrivate";
            const string generatedRoot = privateRoot + "/Generated";
            const string materialRoot = generatedRoot + "/Chapter1Materials";
            const string sceneRoot = privateRoot + "/Scenes/Chapter1";
            const string resourceRoot = privateRoot + "/Resources";
            const string environmentRoot = resourceRoot + "/OriginalChapter1Maps";
            EnsureFolder(privateRoot, "Generated");
            EnsureFolder(generatedRoot, "Chapter1Materials");
            EnsureFolder(privateRoot, "Scenes");
            EnsureFolder(privateRoot + "/Scenes", "Chapter1");
            EnsureFolder(privateRoot, "Resources");
            EnsureFolder(resourceRoot, "OriginalChapter1Maps");

            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { sourceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path).StartsWith("M1", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path)
                .ToArray();
            if (scenePaths.Length != 11) throw new System.Exception($"Expected 11 Chapter 1 scene files, found {scenePaths.Length}");
            var report = new List<string>
            {
                "Twelve Tails Chapter 1 environment generation",
                $"Unity: {Application.unityVersion}",
                $"scenes={scenePaths.Length}"
            };
            foreach (var sourcePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    RemoveMissingScripts(root);
                    ConvertMaterials(root, Path.GetFileNameWithoutExtension(sourcePath), materialRoot);
                }
                var staticRepairs = RepairStaticBatchedMeshes(roots);
                var title = Path.GetFileNameWithoutExtension(sourcePath);
                var convertedPath = $"{sceneRoot}/{title}_URP.unity";
                EditorSceneManager.SaveScene(scene, convertedPath, true);

                var sourceRoot = roots.FirstOrDefault(root => root.name == "SceneObjects" || root.name == "SceneObject");
                if (sourceRoot == null) throw new System.Exception($"{title} has no SceneObjects root");
                var environment = Object.Instantiate(sourceRoot);
                environment.name = title;
                var removedContainers = environment.GetComponentsInChildren<Transform>(true)
                    .Where(item => item != environment.transform && IsDynamicSceneContainer(item.name))
                    .OrderByDescending(HierarchyDepth)
                    .ToArray();
                foreach (var container in removedContainers) Object.DestroyImmediate(container.gameObject);
                var dynamicMeshPlaceholders = environment.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter.sharedMesh == null && IsExpectedDynamicMesh(filter.transform))
                    .Select(filter => filter.gameObject)
                    .Distinct()
                    .ToArray();
                foreach (var placeholder in dynamicMeshPlaceholders) Object.DestroyImmediate(placeholder);
                RemoveMissingScripts(environment);
                var prefabPath = $"{environmentRoot}/{title}.prefab";
                PrefabUtility.SaveAsPrefabAsset(environment, prefabPath);
                var renderers = environment.GetComponentsInChildren<Renderer>(true);
                var missingMeshes = environment.GetComponentsInChildren<MeshFilter>(true).Count(filter => filter.sharedMesh == null) +
                                    environment.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(renderer => renderer.sharedMesh == null);
                var missingMaterials = renderers.Sum(renderer => renderer.sharedMaterials.Count(material => material == null));
                var unsupportedMaterials = renderers.SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null).Distinct()
                    .Count(material => material.shader == null || !material.shader.isSupported ||
                                       material.shader.name == "Hidden/InternalErrorShader");
                var missingScripts = environment.GetComponentsInChildren<Transform>(true)
                    .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
                report.Add($"{title}: staticRepairs={staticRepairs}, removedDynamicContainers={removedContainers.Length}, " +
                           $"removedDynamicMeshPlaceholders={dynamicMeshPlaceholders.Length}, " +
                           $"renderers={renderers.Length}, missingMeshes={missingMeshes}, missingMaterials={missingMaterials}, " +
                           $"unsupportedMaterials={unsupportedMaterials}, missingScripts={missingScripts}, prefab={prefabPath}");
                Object.DestroyImmediate(environment);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var repositoryRoot = Directory.GetParent(Application.dataPath)?.Parent?.FullName ?? ".";
            var artifactDirectory = Path.Combine(repositoryRoot, "artifacts");
            Directory.CreateDirectory(artifactDirectory);
            var reportPath = Path.Combine(artifactDirectory, "legacy-chapter-1-environment-generation.txt");
            File.WriteAllLines(reportPath, report);
            Debug.Log(string.Join("\n", report));
            if (report.Skip(3).Any(line => !line.Contains("missingMeshes=0") ||
                                                   !line.Contains("missingMaterials=0") ||
                                                   !line.Contains("unsupportedMaterials=0") ||
                                                   !line.Contains("missingScripts=0")))
                throw new System.Exception($"Chapter 1 environment generation failed. See {reportPath}");
        }

        private static bool IsDynamicSceneContainer(string name)
        {
            return name.Equals("NPC", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Icons", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("TestControl", System.StringComparison.OrdinalIgnoreCase);
        }

        private static int RepairStaticBatchedMeshes(IEnumerable<GameObject> roots)
        {
            const string meshFolder = "Assets/TwelveTails/LegacyPrivate/Mesh";
            var catalog = AssetDatabase.FindAssets("t:Mesh", new[] { meshFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(path => !IsCollisionMeshPath(path))
                .Select(path => AssetDatabase.LoadAssetAtPath<Mesh>(path))
                .Where(mesh => mesh != null && !mesh.name.StartsWith("Combined Mesh", System.StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();
            var repairs = 0;
            foreach (var filter in roots.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)))
            {
                if (filter.sharedMesh == null ||
                    !filter.sharedMesh.name.StartsWith("Combined Mesh", System.StringComparison.OrdinalIgnoreCase)) continue;
                var objectKey = NormalizedMeshName(filter.name);
                var objectBase = Regex.Replace(objectKey, "[0-9]+$", string.Empty);
                var ranked = catalog.Select(mesh => new
                    {
                        Mesh = mesh,
                        Key = NormalizedMeshName(mesh.name)
                    })
                    .Select(item => new
                    {
                        item.Mesh,
                        Score = item.Key == objectKey ? 4 :
                            item.Key == objectBase ? 3 :
                            item.Key.EndsWith(objectKey, System.StringComparison.Ordinal) ? 2 :
                            Regex.Replace(item.Key, "[0-9]+$", string.Empty) == objectBase ? 1 : 0
                    })
                    .Where(item => item.Score > 0)
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Mesh.name)
                    .ToArray();
                if (ranked.Length == 0) throw new System.Exception($"No individual mesh matches static object {HierarchyPath(filter.transform)}");
                var best = ranked.Where(item => item.Score == ranked[0].Score).ToArray();
                if (best.Length != 1)
                    throw new System.Exception($"Ambiguous individual meshes for {HierarchyPath(filter.transform)}: " +
                                               string.Join(",", best.Select(item => item.Mesh.name)));
                filter.sharedMesh = best[0].Mesh;
                repairs++;
            }
            return repairs;
        }

        private static string NormalizedMeshName(string value)
        {
            value = Regex.Replace(value, @"(?i)(^|[^a-z0-9])(tri|model|collision|collider|n)(?=$|[^a-z0-9])", " ");
            return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private static bool IsCollisionMeshPath(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return name.Contains("collision") || name.Contains("collider") || name.EndsWith("_c");
        }
    }
}
