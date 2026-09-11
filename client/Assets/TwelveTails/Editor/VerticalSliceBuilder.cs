using System.IO;
using TwelveTails.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using Object = UnityEngine.Object;
using System.Linq;
using UnityEditor.Animations;

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
            LegacyAssetValidator.GenerateOriginalCharacterPrefabsIfAvailable();
            GenerateCharacterPrefabs();
            var catalog = LoadCatalog();
            Directory.CreateDirectory("Assets/TwelveTails/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var originalMap = Resources.Load<GameObject>("OriginalMaps/M101_CarronHarvest");
            if (originalMap != null)
            {
                var environment = (GameObject)PrefabUtility.InstantiatePrefab(originalMap);
                environment.name = "Original M101 Carron Harvest";
            }
            else
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Training Ground";
                ground.transform.localScale = new Vector3(4f, 1f, 4f);
            }

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = GroundedPosition(Position(catalog.spawns, "spawn.player"));
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

            var enemy = new GameObject("Carron - Training Target");
            enemy.transform.position = GroundedPosition(Position(catalog.spawns, "spawn.dummy"), .05f);
            var enemyCollider = enemy.AddComponent<CapsuleCollider>();
            enemyCollider.center = new Vector3(0f, .75f, 0f);
            enemyCollider.height = 1.5f;
            enemyCollider.radius = .65f;
            var originalCarron = Resources.Load<GameObject>("OriginalMonsters/Carron");
            if (originalCarron != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(originalCarron, enemy.transform);
                visual.name = "Original Carron Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Fallback Training Target Visual";
                visual.transform.SetParent(enemy.transform, false);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
            }
            enemy.AddComponent<Health>().Configure(30);
            enemy.AddComponent<EnemyTarget>().Configure(quest, progress, saves);

            var npc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            npc.name = "Guide NPC";
            npc.transform.position = GroundedPosition(Position(catalog.npcs, "npc.guide"));
            npc.transform.localScale = new Vector3(1f, 2f, 1f);

            foreach (var definition in catalog.portals)
            {
                var portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                portal.name = $"Portal {definition.id} -> {definition.destination_map}";
                portal.transform.position = GroundedPosition(ToVector(definition.position), .1f);
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

        private static Vector3 GroundedPosition(Vector3 position, float offset = 1f)
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return position;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + offset;
            return position;
        }

        [MenuItem("12 Tails/Generate Character Prefabs")]
        public static void GenerateCharacterPrefabs()
        {
            const string prefabDirectory = "Assets/TwelveTails/Resources/Characters";
            const string materialDirectory = "Assets/TwelveTails/Generated/Materials";
            const string controllerDirectory = "Assets/TwelveTails/Generated/Controllers";
            Directory.CreateDirectory(prefabDirectory);
            Directory.CreateDirectory(materialDirectory);
            Directory.CreateDirectory(controllerDirectory);
            foreach (var id in CharacterRoster.Ids)
            {
                var title = char.ToUpperInvariant(id[0]) + id.Substring(1);
                var fbxPath = $"Assets/TwelveTails/Art/Characters/{title}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (model != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    instance.name = title;
                    var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                        .Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
                    var idle = clips.FirstOrDefault(clip => clip.name.Contains("Idle"));
                    var run = clips.FirstOrDefault(clip => clip.name.Contains("Run"));
                    var attack = clips.FirstOrDefault(clip => clip.name.Contains("Attack"));
                    if (idle == null || run == null || attack == null)
                        throw new InvalidDataException($"{title}.fbx must contain Idle, Run, and Attack clips.");
                    var controllerPath = $"{controllerDirectory}/{title}.controller";
                    AssetDatabase.DeleteAsset(controllerPath);
                    var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                    controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                    controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
                    var machine = controller.layers[0].stateMachine;
                    var idleState = machine.AddState("Idle"); idleState.motion = idle;
                    var runState = machine.AddState("Run"); runState.motion = run;
                    var attackState = machine.AddState("Attack"); attackState.motion = attack;
                    machine.defaultState = idleState;
                    var toRun = idleState.AddTransition(runState); toRun.hasExitTime = false; toRun.AddCondition(AnimatorConditionMode.Greater, .1f, "Speed");
                    var toIdle = runState.AddTransition(idleState); toIdle.hasExitTime = false; toIdle.AddCondition(AnimatorConditionMode.Less, .1f, "Speed");
                    var toAttack = machine.AddAnyStateTransition(attackState); toAttack.hasExitTime = false; toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                    var attackDone = attackState.AddTransition(idleState); attackDone.hasExitTime = true; attackDone.exitTime = .9f;
                    var animator = instance.GetComponent<Animator>();
                    if (animator == null) animator = instance.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    instance.AddComponent<AnimatorMotionDriver>();
                    PrefabUtility.SaveAsPrefabAsset(instance, $"{prefabDirectory}/{title}.prefab");
                    Object.DestroyImmediate(instance);
                    continue;
                }
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
                visual.name = title;
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
