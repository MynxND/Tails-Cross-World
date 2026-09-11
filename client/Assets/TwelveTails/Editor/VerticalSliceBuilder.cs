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
        private const string ChapterMenuScenePath = "Assets/TwelveTails/Scenes/ChapterMenu.unity";

        [Serializable] private sealed class Catalog { public Spawn[] spawns = Array.Empty<Spawn>(); public Portal[] portals = Array.Empty<Portal>(); public Npc[] npcs = Array.Empty<Npc>(); }
        [Serializable] private sealed class SkillCatalog { public SkillJson[] skills = Array.Empty<SkillJson>(); }
        [Serializable] private sealed class ProfileCatalog { public ProfileJson[] profiles = Array.Empty<ProfileJson>(); }
        [Serializable] private sealed class MissionCatalog { public MissionJson[] missions = Array.Empty<MissionJson>(); }
        [Serializable] private sealed class MissionJson
        {
            public string id = string.Empty;
            public string scene_name = string.Empty;
            public string environment_resource = string.Empty;
            public PositionJson player_spawn = new();
            public ObjectiveJson objective = new();
            public RewardJson reward = new();
            public ActorGroupJson[] actors = Array.Empty<ActorGroupJson>();
            public InteractableJson[] interactables = Array.Empty<InteractableJson>();
        }
        [Serializable] private sealed class ObjectiveJson { public string kind = string.Empty; public string target_id = string.Empty; public int count; }
        [Serializable] private sealed class RewardJson { public int experience; public int potions; }
        [Serializable] private sealed class ActorGroupJson
        {
            public string id = string.Empty;
            public string entity_id = string.Empty;
            public string prefab_resource = string.Empty;
            public int health;
            public float move_speed;
            public int attack_damage;
            public FirstDamageSpawnJson spawn_on_first_damage = new();
            public PositionJson[] positions = Array.Empty<PositionJson>();
        }
        [Serializable] private sealed class FirstDamageSpawnJson
        {
            public bool enabled;
            public string green_prefab_resource = string.Empty;
            public string red_prefab_resource = string.Empty;
        }
        [Serializable] private sealed class InteractableJson
        {
            public string id = string.Empty;
            public string target_id = string.Empty;
            public string prefab_resource = string.Empty;
            public PositionJson position = new();
        }
        [Serializable] private sealed class PositionJson { public float x; public float y; public float z; public Vector3 Vector => new(x, y, z); }
        [Serializable] private sealed class ProfileJson
        {
            public string character_id = string.Empty;
            public ProfileSkillJson[] skills = Array.Empty<ProfileSkillJson>();
        }
        [Serializable] private sealed class ProfileSkillJson
        {
            public string skill_id = string.Empty;
            public string clip_name = string.Empty;
        }
        [Serializable] private sealed class SkillJson
        {
            public string id = string.Empty;
            public string character_id = string.Empty;
            public string animation_clip = string.Empty;
            public int damage;
            public float cooldown_seconds;
            public float hit_delay_seconds;
            public int hit_count;
            public float hit_interval_seconds;
            public float action_duration_seconds;
            public float combo_window_start_seconds;
            public float combo_window_end_seconds;
            public string combo_next_skill_id = string.Empty;
            public int resource_cost;
            public float range;
            public float projectile_speed;
            public float projectile_lifetime_seconds;
            public float projectile_homing_radians_per_second;
            public float impact_radius;
            public string status_effect_id = string.Empty;
            public float status_duration_seconds;
            public float status_tick_seconds;
            public int status_damage_per_tick;
            public float status_movement_multiplier = 1f;
        }
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
            var skills = player.AddComponent<SkillExecutor>();
            skills.Configure(LoadSkills());
            skills.ConfigureAnimations(LoadAnimationBindings());
            var playerHealth = player.AddComponent<Health>();
            playerHealth.Configure(100);
            player.AddComponent<CharacterSelector>();
            player.AddComponent<DefeatAnimationDriver>().ObserveHealth(false);
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
            enemy.AddComponent<DefeatAnimationDriver>();

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

        [MenuItem("12 Tails/Build Mupo Round Up")]
        public static void BuildMupoRoundUp()
        {
            const string scenePath = "Assets/TwelveTails/Scenes/MupoRoundUp.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environmentPrefab = Resources.Load<GameObject>("OriginalChapter1Maps/M102_MupoRoundUp");
            if (environmentPrefab == null) throw new InvalidDataException("M102 environment prefab is missing.");
            var environment = (GameObject)PrefabUtility.InstantiatePrefab(environmentPrefab);
            environment.name = "Original M102 Mupo Round Up";

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = GroundedPosition(new Vector3(-23f, 50f, -19f));
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<MeleeAttack>();
            var skills = player.AddComponent<SkillExecutor>();
            skills.Configure(LoadSkills());
            skills.ConfigureAnimations(LoadAnimationBindings());
            var playerHealth = player.AddComponent<Health>();
            playerHealth.Configure(100);
            player.AddComponent<CharacterSelector>();
            player.AddComponent<DefeatAnimationDriver>().ObserveHealth(false);
            var quest = player.AddComponent<QuestProgress>();
            var progress = player.AddComponent<PlayerProgress>();
            var saves = player.AddComponent<SaveCoordinator>();
            saves.Configure(progress, quest);
            var mission = player.AddComponent<MupoHerdMission>();
            mission.Configure(progress, saves, 6);
            saves.Configure(progress, quest, mission);
            var lan = player.AddComponent<LanGameClient>();
            lan.ConfigureMap("map.m102_mupo_round_up");

            var mupoPrefab = Resources.Load<GameObject>("OriginalMonsters/Mupo");
            if (mupoPrefab == null) throw new InvalidDataException("Original Mupo prefab is missing.");
            var mupoPositions = new[]
            {
                new Vector3(13.975708f, 50.10121f, 3.986961f),
                new Vector3(13.973831f, 50.10121f, 22.58904f),
                new Vector3(-3.303841f, 50.179405f, 7.159367f),
                new Vector3(.744713f, 50.101852f, 17.176952f),
                new Vector3(8.046227f, 50.101852f, 12.605358f),
                new Vector3(-9.783875f, 50.10121f, 26.21101f)
            };
            for (var index = 0; index < mupoPositions.Length; index++)
            {
                var root = new GameObject($"Mupo {index + 1}");
                root.transform.position = GroundedPosition(mupoPositions[index], .05f);
                var collider = root.AddComponent<CharacterController>();
                collider.height = 2f;
                collider.radius = .65f;
                root.AddComponent<Health>().Configure(30);
                var target = root.AddComponent<MupoHerdTarget>();
                target.ConfigureId($"mupo-{index + 1}");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(mupoPrefab, root.transform);
                visual.name = "Original Mupo Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                mission.RegisterTarget(target);
            }

            var pen = new GameObject("Mupo Pen Trigger");
            pen.transform.position = GroundedPosition(new Vector3(13f, 50f, 13f), .05f);
            var penCollider = pen.AddComponent<BoxCollider>();
            penCollider.isTrigger = true;
            penCollider.size = new Vector3(8f, 3f, 8f);
            var penTrigger = pen.AddComponent<MupoPenTrigger>();
            penTrigger.Configure(mission);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Pen Marker";
            marker.transform.SetParent(pen.transform, false);
            marker.transform.localScale = new Vector3(4f, .03f, 4f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial.color = new Color(.95f, .75f, .15f);

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
            var hud = new GameObject("Mupo Round Up HUD").AddComponent<PrototypeHud>();
            hud.ConfigureMupo(mission, progress, saves);

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(scenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {scenePath}");
        }

        [MenuItem("12 Tails/Build M103 Bug Trouble")]
        public static void BuildBugTrouble()
        {
            const string scenePath = "Assets/TwelveTails/Scenes/BugTrouble.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environmentPrefab = Resources.Load<GameObject>("OriginalChapter1Maps/M103_BugTrouble");
            var goatPrefab = Resources.Load<GameObject>("OriginalNpcs/GoatFarmer");
            var stingBugPrefab = Resources.Load<GameObject>("OriginalMonsters/StingBug");
            if (environmentPrefab == null) throw new InvalidDataException("M103 environment prefab is missing.");
            if (goatPrefab == null) throw new InvalidDataException("Original GoatFarmer prefab is missing.");
            if (stingBugPrefab == null) throw new InvalidDataException("Original StingBug prefab is missing.");
            var environment = (GameObject)PrefabUtility.InstantiatePrefab(environmentPrefab);
            environment.name = "Original M103 Bug Trouble";

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = GroundedPosition(new Vector3(130f, 49f, 132f));
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<MeleeAttack>();
            var skills = player.AddComponent<SkillExecutor>();
            skills.Configure(LoadSkills());
            skills.ConfigureAnimations(LoadAnimationBindings());
            player.AddComponent<Health>().Configure(100);
            player.AddComponent<CharacterSelector>();
            player.AddComponent<DefeatAnimationDriver>().ObserveHealth(false);
            var quest = player.AddComponent<QuestProgress>();
            quest.Configure("monster.stingbug", 5);
            var progress = player.AddComponent<PlayerProgress>();
            var saves = player.AddComponent<SaveCoordinator>();
            saves.Configure(progress, quest);

            var protectedActor = new GameObject("Protected Goat Farmer");
            protectedActor.transform.position = GroundedPosition(new Vector3(137.0138f, 48.652626f, 137.84518f), .05f);
            var protectedCollider = protectedActor.AddComponent<CapsuleCollider>();
            protectedCollider.center = new Vector3(0f, .8f, 0f);
            protectedCollider.height = 1.6f;
            protectedCollider.radius = .5f;
            var protectedHealth = protectedActor.AddComponent<Health>();
            protectedHealth.Configure(80);
            var goatVisual = (GameObject)PrefabUtility.InstantiatePrefab(goatPrefab, protectedActor.transform);
            goatVisual.name = "Original GoatFarmer Visual";
            goatVisual.transform.localPosition = Vector3.zero;
            goatVisual.transform.localRotation = Quaternion.identity;

            var mission = player.AddComponent<DefeatAndProtectMission>();
            mission.Configure(protectedHealth, "Goat Farmer", quest, progress, saves, 40, 2);
            var spawnOffsets = new[]
            {
                new Vector3(-8f, 0f, 6f),
                new Vector3(8f, 0f, 7f),
                new Vector3(-10f, 0f, -4f),
                new Vector3(10f, 0f, -5f),
                new Vector3(0f, 0f, 11f)
            };
            for (var index = 0; index < spawnOffsets.Length; index++)
            {
                var enemy = new GameObject($"StingBug {index + 1}");
                enemy.transform.position = GroundedPosition(protectedActor.transform.position + spawnOffsets[index], .4f);
                var controller = enemy.AddComponent<CharacterController>();
                controller.height = 1.2f;
                controller.radius = .55f;
                enemy.AddComponent<Health>().Configure(24);
                enemy.AddComponent<EnemyTarget>().ConfigureForMission("monster.stingbug", mission);
                enemy.AddComponent<DefeatAnimationDriver>();
                enemy.AddComponent<MonsterChase>().Configure(protectedActor.transform, 1.5f, 5);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(stingBugPrefab, enemy.transform);
                visual.name = "Original StingBug Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
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
            new GameObject("Bug Trouble HUD").AddComponent<PrototypeHud>().ConfigureProtect(mission, progress, saves);

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/TwelveTails/Scenes/MupoRoundUp.unity", true),
                new EditorBuildSettingsScene(scenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {scenePath}");
        }

        [MenuItem("12 Tails/Build M104 Stingbug Nest")]
        public static void BuildStingbugNest() => BuildManifestMission("mission.m104_stingbug_nest");

        [MenuItem("12 Tails/Build M101 Carron Hunt")]
        public static void BuildCarronHarvest() => BuildManifestMission("mission.m101_carron_hunt");

        [MenuItem("12 Tails/Build M105 Needle Cave")]
        public static void BuildNeedleCave() => BuildManifestMission("mission.m105_needle_cave");

        [MenuItem("12 Tails/Build M106 Boldas Recruitment")]
        public static void BuildBoldasRecruitment() => BuildManifestMission("mission.m106_boldas_recruitment");

        [MenuItem("12 Tails/Build M107 Request From Alcacia")]
        public static void BuildRequestFromAlcacia() => BuildManifestMission("mission.m107_request_from_alcacia");

        [MenuItem("12 Tails/Build M108 One On One Bout")]
        public static void BuildOneOnOneBout() => BuildManifestMission("mission.m108_one_on_one_bout");

        private static void BuildManifestMission(string missionId)
        {
            var definition = LoadMission(missionId);
            if (definition.objective.kind != "defeat" && definition.objective.kind != "interact" && definition.objective.kind != "knockout" && definition.objective.kind != "duel")
                throw new InvalidDataException($"Unsupported mission objective: {definition.objective.kind}");
            var environmentPrefab = Resources.Load<GameObject>(definition.environment_resource);
            if (environmentPrefab == null) throw new InvalidDataException($"Mission environment is missing: {definition.environment_resource}");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environment = (GameObject)PrefabUtility.InstantiatePrefab(environmentPrefab);
            environment.name = $"Original {definition.id} Environment";

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = GroundedPosition(definition.player_spawn.Vector);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<MeleeAttack>();
            var skills = player.AddComponent<SkillExecutor>();
            skills.Configure(LoadSkills());
            skills.ConfigureAnimations(LoadAnimationBindings());
            var playerHealth = player.AddComponent<Health>();
            playerHealth.Configure(100);
            player.AddComponent<CharacterSelector>();
            player.AddComponent<DefeatAnimationDriver>().ObserveHealth(false);
            var quest = player.AddComponent<QuestProgress>();
            var objectiveVerb = definition.objective.kind == "interact" ? "Talk to" : definition.objective.kind == "knockout" ? "Knock out" : "Defeat";
            quest.Configure(definition.objective.target_id, definition.objective.count, objectiveVerb);
            var progress = player.AddComponent<PlayerProgress>();
            var saves = player.AddComponent<SaveCoordinator>();
            saves.Configure(progress, quest);
            DefeatAndProtectMission duelMission = null;
            if (definition.objective.kind == "duel")
            {
                duelMission = player.AddComponent<DefeatAndProtectMission>();
                duelMission.Configure(
                    playerHealth,
                    "Player",
                    quest,
                    progress,
                    saves,
                    definition.reward.experience,
                    definition.reward.potions);
            }

            foreach (var group in definition.actors)
            {
                var actorPrefab = Resources.Load<GameObject>(group.prefab_resource);
                if (actorPrefab == null) throw new InvalidDataException($"Mission actor prefab is missing: {group.prefab_resource}");
                for (var index = 0; index < group.positions.Length; index++)
                {
                    var actor = new GameObject($"{group.id} {index + 1}");
                    actor.transform.position = GroundedPosition(group.positions[index].Vector, .05f);
                    var controller = actor.AddComponent<CharacterController>();
                    controller.height = group.move_speed > 0f ? 1.2f : 2f;
                    controller.radius = .6f;
                    actor.AddComponent<Health>().Configure(group.health);
                    if (definition.objective.kind == "duel" && group.entity_id == definition.objective.target_id)
                    {
                        actor.AddComponent<EnemyTarget>().ConfigureForMission(group.entity_id, duelMission);
                    }
                    else if (definition.objective.kind == "knockout" && group.entity_id == definition.objective.target_id)
                    {
                        actor.AddComponent<KnockoutObjectiveTarget>().Configure(
                            group.entity_id,
                            quest,
                            progress,
                            saves,
                            definition.reward.experience,
                            definition.reward.potions);
                        actor.AddComponent<KnockoutAnimationDriver>();
                    }
                    else
                    {
                        actor.AddComponent<EnemyTarget>().ConfigureEntity(
                            group.entity_id,
                            quest,
                            progress,
                            saves,
                            definition.reward.experience,
                            definition.reward.potions);
                    }
                    if (group.move_speed > 0f)
                        actor.AddComponent<MonsterChase>().Configure(player.transform, group.move_speed, group.attack_damage);
                    if (group.spawn_on_first_damage.enabled)
                    {
                        var greenPrefab = Resources.Load<GameObject>(group.spawn_on_first_damage.green_prefab_resource);
                        var redPrefab = Resources.Load<GameObject>(group.spawn_on_first_damage.red_prefab_resource);
                        if (greenPrefab == null || redPrefab == null)
                            throw new InvalidDataException($"First-damage spawn prefab is missing for actor group: {group.id}");
                        actor.AddComponent<SpawnOnFirstDamage>().Configure(greenPrefab, redPrefab, player.transform, quest, progress, saves);
                    }
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(actorPrefab, actor.transform);
                    visual.name = $"Original {group.entity_id} Visual";
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    if (actor.GetComponent<EnemyTarget>() != null)
                        actor.AddComponent<DefeatAnimationDriver>();
                    var animation = visual.GetComponentInChildren<Animation>(true);
                    if (animation != null && animation.GetComponent<LegacyAnimationDriver>() == null)
                        animation.gameObject.AddComponent<LegacyAnimationDriver>();
                }
            }

            foreach (var definitionInteractable in definition.interactables)
            {
                var interactablePrefab = Resources.Load<GameObject>(definitionInteractable.prefab_resource);
                if (interactablePrefab == null)
                    throw new InvalidDataException($"Mission interactable prefab is missing: {definitionInteractable.prefab_resource}");
                var interactable = new GameObject(definitionInteractable.id);
                interactable.transform.position = GroundedPosition(definitionInteractable.position.Vector, .05f);
                var trigger = interactable.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 2f;
                interactable.AddComponent<QuestInteractionTarget>().Configure(
                    definitionInteractable.target_id,
                    quest,
                    progress,
                    saves,
                    definition.reward.experience,
                    definition.reward.potions);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(interactablePrefab, interactable.transform);
                visual.name = $"Original {definitionInteractable.target_id} Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
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
            var hud = new GameObject($"{definition.id} HUD").AddComponent<PrototypeHud>();
            if (duelMission != null) hud.ConfigureProtect(duelMission, progress, saves);
            else hud.Configure(quest, progress, saves);

            var scenePath = $"Assets/TwelveTails/Scenes/{definition.scene_name}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            var scenes = EditorBuildSettings.scenes
                .Where(item => item.path != scenePath)
                .Concat(new[] { new EditorBuildSettingsScene(scenePath, true) })
                .ToArray();
            EditorBuildSettings.scenes = scenes;
            AssetDatabase.SaveAssets();
            Debug.Log($"Created manifest mission {definition.id}: {scenePath}");
        }

        [MenuItem("12 Tails/Build Chapter Menu")]
        public static void BuildChapterMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .12f, .16f);
            cameraObject.AddComponent<AudioListener>();
            new GameObject("Chapter Menu").AddComponent<ChapterMenu>();
            EditorSceneManager.SaveScene(scene, ChapterMenuScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ChapterMenuScenePath, true),
                new EditorBuildSettingsScene("Assets/TwelveTails/Scenes/CarronHarvest.unity", true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"Created chapter menu: {ChapterMenuScenePath}");
        }

        private static Catalog LoadCatalog()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content", "v1", "training_chapter.json"));
            if (!File.Exists(path)) throw new FileNotFoundException("Phase 4 content catalog was not found.", path);
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(path));
            if (catalog == null || catalog.spawns.Length == 0) throw new InvalidDataException("Phase 4 content catalog is invalid.");
            return catalog;
        }

        private static MissionJson LoadMission(string missionId)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content", "v1", "chapter1_missions.json"));
            if (!File.Exists(path)) throw new FileNotFoundException("Chapter 1 mission catalog was not found.", path);
            var catalog = JsonUtility.FromJson<MissionCatalog>(File.ReadAllText(path));
            var mission = catalog?.missions?.FirstOrDefault(item => item.id == missionId);
            if (mission == null) throw new InvalidDataException($"Mission definition is missing: {missionId}");
            if (mission.objective.count < 1 || (mission.actors.Length == 0 && mission.objective.kind != "interact"))
                throw new InvalidDataException($"Mission definition is incomplete: {missionId}");
            return mission;
        }

        private static SkillDefinition[] LoadSkills()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content", "v1", "skills.json"));
            if (!File.Exists(path)) throw new FileNotFoundException("Skill catalog was not found.", path);
            var catalog = JsonUtility.FromJson<SkillCatalog>(File.ReadAllText(path));
            if (catalog == null || catalog.skills == null || catalog.skills.Length == 0)
                throw new InvalidDataException("Skill catalog is invalid.");
            return catalog.skills.Select(skill => new SkillDefinition
            {
                id = skill.id,
                characterId = skill.character_id,
                animationClip = skill.animation_clip,
                damage = skill.damage,
                cooldownSeconds = skill.cooldown_seconds,
                hitDelaySeconds = skill.hit_delay_seconds,
                hitCount = skill.hit_count,
                hitIntervalSeconds = skill.hit_interval_seconds,
                actionDurationSeconds = skill.action_duration_seconds,
                comboWindowStartSeconds = skill.combo_window_start_seconds,
                comboWindowEndSeconds = skill.combo_window_end_seconds,
                comboNextSkillId = skill.combo_next_skill_id,
                resourceCost = skill.resource_cost,
                range = skill.range,
                projectileSpeed = skill.projectile_speed,
                projectileLifetimeSeconds = skill.projectile_lifetime_seconds,
                projectileHomingRadiansPerSecond = skill.projectile_homing_radians_per_second,
                impactRadius = skill.impact_radius,
                statusEffectId = skill.status_effect_id,
                statusDurationSeconds = skill.status_duration_seconds,
                statusTickSeconds = skill.status_tick_seconds,
                statusDamagePerTick = skill.status_damage_per_tick,
                statusMovementMultiplier = skill.status_movement_multiplier
            }).ToArray();
        }

        private static SkillAnimationBinding[] LoadAnimationBindings()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content", "v1", "animation_profiles.json"));
            if (!File.Exists(path)) throw new FileNotFoundException("Animation profile catalog was not found.", path);
            var catalog = JsonUtility.FromJson<ProfileCatalog>(File.ReadAllText(path));
            if (catalog == null || catalog.profiles == null || catalog.profiles.Length == 0)
                throw new InvalidDataException("Animation profile catalog is invalid.");
            return catalog.profiles.SelectMany(profile => profile.skills.Select(skill => new SkillAnimationBinding
            {
                characterId = profile.character_id,
                skillId = skill.skill_id,
                clipName = skill.clip_name
            })).ToArray();
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
            var animationBindings = LoadAnimationBindings();
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
                    foreach (var binding in animationBindings.Where(value => value.characterId == id))
                    {
                        var skillClip = clips.FirstOrDefault(clip => clip.name == binding.clipName);
                        if (skillClip == null)
                        {
                            skillClip = attack;
                            Debug.LogWarning($"{title}.fbx is missing mapped clip {binding.clipName}; using {attack.name} until the legacy clip is retargeted.");
                        }
                        var skillState = machine.AddState(binding.clipName);
                        skillState.motion = skillClip;
                    }
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
            BuildCarronHarvest();
            BuildChapterMenu();
            BuildWindowsPlayer(
                new[] { ChapterMenuScenePath, "Assets/TwelveTails/Scenes/CarronHarvest.unity" },
                "Builds/Windows/TwelveTailsPrototype.exe");
        }

        [MenuItem("12 Tails/Build M103 Windows")]
        public static void BuildBugTroubleWindows()
        {
            BuildBugTrouble();
            BuildWindowsPlayer("Assets/TwelveTails/Scenes/BugTrouble.unity", "Builds/Windows-M103/TwelveTailsM103.exe");
        }

        [MenuItem("12 Tails/Build M104 Windows")]
        public static void BuildStingbugNestWindows()
        {
            BuildStingbugNest();
            BuildWindowsPlayer("Assets/TwelveTails/Scenes/StingbugNest.unity", "Builds/Windows-M104/TwelveTailsM104.exe");
        }

        [MenuItem("12 Tails/Build M105 Windows")]
        public static void BuildNeedleCaveWindows()
        {
            BuildNeedleCave();
            BuildWindowsPlayer("Assets/TwelveTails/Scenes/NeedleCave.unity", "Builds/Windows-M105/TwelveTailsM105.exe");
        }

        [MenuItem("12 Tails/Build M106 Windows")]
        public static void BuildBoldasRecruitmentWindows()
        {
            BuildBoldasRecruitment();
            BuildWindowsPlayer("Assets/TwelveTails/Scenes/BoldasRecruitment.unity", "Builds/Windows-M106/TwelveTailsM106.exe");
        }

        [MenuItem("12 Tails/Build M107 Windows")]
        public static void BuildRequestFromAlcaciaWindows()
        {
            BuildRequestFromAlcacia();
            BuildWindowsPlayer("Assets/TwelveTails/Scenes/RequestFromAlcacia.unity", "Builds/Windows-M107/TwelveTailsM107.exe");
        }

        [MenuItem("12 Tails/Build M108 Windows")]
        public static void BuildOneOnOneBoutWindows()
        {
            BuildOneOnOneBout();
            BuildWindowsPlayer("Assets/TwelveTails/Scenes/OneOnOneBout.unity", "Builds/Windows-M108/TwelveTailsM108.exe");
        }

        private static void BuildWindowsPlayer(string startupScene, string outputPath) =>
            BuildWindowsPlayer(new[] { startupScene }, outputPath);

        private static void BuildWindowsPlayer(string[] scenes, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
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
