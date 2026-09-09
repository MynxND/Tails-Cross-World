using System.IO;
using TwelveTails.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwelveTails.EditorTools
{
    public static class VerticalSliceBuilder
    {
        private const string ScenePath = "Assets/TwelveTails/Scenes/TrainingGround.unity";

        [MenuItem("12 Tails/Build Training Ground")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/TwelveTails/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Training Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, -4f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<MeleeAttack>();
            player.AddComponent<Health>().Configure(100);
            var quest = player.AddComponent<QuestProgress>();
            var progress = player.AddComponent<PlayerProgress>();
            var saves = player.AddComponent<SaveCoordinator>();
            saves.Configure(progress, quest);
            player.AddComponent<LanGameClient>();

            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Training Dummy";
            enemy.transform.position = new Vector3(0f, 1f, 3f);
            enemy.AddComponent<Health>().Configure(30);
            enemy.AddComponent<EnemyTarget>().Configure(quest, progress, saves);

            var npc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            npc.name = "Guide NPC";
            npc.transform.position = new Vector3(-4f, 1f, 0f);
            npc.transform.localScale = new Vector3(1f, 2f, 1f);

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
