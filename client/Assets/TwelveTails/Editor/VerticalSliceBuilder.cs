using System.IO;
using TwelveTails.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using Object = UnityEngine.Object;

namespace TwelveTails.EditorTools
{
    public static class VerticalSliceBuilder
    {
        private const string ScenePath = "Assets/TwelveTails/Scenes/TrainingGround.unity";

        [Serializable] private sealed class Catalog { public Spawn[] spawns = Array.Empty<Spawn>(); public Portal[] portals = Array.Empty<Portal>(); public Npc[] npcs = Array.Empty<Npc>(); }
        [Serializable] private sealed class Spawn { public string id = string.Empty; public float[] position = Array.Empty<float>(); }
        [Serializable] private sealed class Portal { public string id = string.Empty; public string destination_map = string.Empty; public float[] position = Array.Empty<float>(); }
        [Serializable] private sealed class Npc { public string id = string.Empty; public float[] position = Array.Empty<float>(); }

        [MenuItem("12 Tails/Build Training Ground")]
        public static void Build()
        {
            GenerateCharacterPrefabs();
            var catalog = LoadCatalog();
            Directory.CreateDirectory("Assets/TwelveTails/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Training Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = Position(catalog.spawns, "spawn.player");
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<MeleeAttack>();
            player.AddComponent<Health>().Configure(100);
            player.AddComponent<CharacterSelector>();
            var quest = player.AddComponent<QuestProgress>();
            var progress = player.AddComponent<PlayerProgress>();
            var saves = player.AddComponent<SaveCoordinator>();
            saves.Configure(progress, quest);
            player.AddComponent<LanGameClient>();

            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Training Dummy";
            enemy.transform.position = Position(catalog.spawns, "spawn.dummy");
            enemy.AddComponent<Health>().Configure(30);
            enemy.AddComponent<EnemyTarget>().Configure(quest, progress, saves);

            var npc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            npc.name = "Guide NPC";
            npc.transform.position = Position(catalog.npcs, "npc.guide");
            npc.transform.localScale = new Vector3(1f, 2f, 1f);

            foreach (var definition in catalog.portals)
            {
                var portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                portal.name = $"Portal {definition.id} -> {definition.destination_map}";
                portal.transform.position = ToVector(definition.position);
                portal.transform.localScale = new Vector3(1.5f, .1f, 1.5f);
                portal.GetComponent<Renderer>().sharedMaterial.color = new Color(.2f, .7f, 1f);
            }

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CameraFollow>().Configure(player.transform);
            cameraObject.transform.position = player.transform.position + new Vector3(0f, 8f, -10f);

            var hud = new GameObject("Prototype HUD").AddComponent<PrototypeHud>();
            hud.Configure(quest, progress, saves);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {ScenePath}");
        }

        private static Catalog LoadCatalog()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content", "v1", "training_chapter.json"));
            if (!File.Exists(path)) throw new FileNotFoundException("Phase 4 content catalog was not found.", path);
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(path));
            if (catalog == null || catalog.spawns.Length == 0) throw new InvalidDataException("Phase 4 content catalog is invalid.");
            return catalog;
        }

        private static Vector3 Position(Spawn[] definitions, string id)
        {
            foreach (var definition in definitions) if (definition.id == id) return ToVector(definition.position);
            throw new InvalidDataException($"Missing spawn: {id}");
        }

        private static Vector3 Position(Npc[] definitions, string id)
        {
            foreach (var definition in definitions) if (definition.id == id) return ToVector(definition.position);
            throw new InvalidDataException($"Missing NPC: {id}");
        }

        private static Vector3 ToVector(float[] value) => new(value[0], value[1], value[2]);

        [MenuItem("12 Tails/Generate Character Prefabs")]
        public static void GenerateCharacterPrefabs()
        {
            const string prefabDirectory = "Assets/TwelveTails/Resources/Characters";
            const string materialDirectory = "Assets/TwelveTails/Generated/Materials";
            Directory.CreateDirectory(prefabDirectory);
            Directory.CreateDirectory(materialDirectory);
            foreach (var id in CharacterRoster.Ids)
            {
                var temporary = new GameObject($"{id} prefab source");
                var visual = ProceduralCharacter.Create(id, temporary.transform);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    var original = renderer.sharedMaterial.color;
                    var safeName = renderer.name.Replace(' ', '_');
                    var materialPath = $"{materialDirectory}/{id}_{safeName}.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = original };
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    else material.color = original;
                    renderer.sharedMaterial = material;
                }
                visual.transform.SetParent(null, false);
                visual.name = char.ToUpperInvariant(id[0]) + id.Substring(1);
                PrefabUtility.SaveAsPrefabAsset(visual, $"{prefabDirectory}/{visual.name}.prefab");
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(temporary);
            }
            AssetDatabase.SaveAssets();
        }

        public static void BuildWindows()
        {
            Build();
            Directory.CreateDirectory("Builds/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/TwelveTailsPrototype.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception($"Windows build failed: {report.summary.result}");
            Debug.Log($"Windows prototype built: {report.summary.outputPath}");
        }
    }
}
