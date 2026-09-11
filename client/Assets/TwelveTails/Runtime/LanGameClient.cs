using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelveTails.Gameplay
{
    public sealed class LanGameClient : MonoBehaviour
    {
        [Serializable] private sealed class Request
        {
            public int protocol_version = 1;
            public string kind = string.Empty;
            public string account_id = string.Empty;
            public string token = string.Empty;
            public string lobby_id = string.Empty;
            public string map_id = string.Empty;
            public string mupo_id = string.Empty;
            public string actor_id = string.Empty;
            public string character_id = string.Empty;
            public string skill_id = string.Empty;
            public string target_id = string.Empty;
            public int sequence;
            public float[] direction = Array.Empty<float>();
            public float[] aim = Array.Empty<float>();
        }
        [Serializable] private sealed class Response { public bool ok; public string error = string.Empty; public Result result = null!; }
        [Serializable] private sealed class Result
        {
            public string token = string.Empty;
            public string account_id = string.Empty;
            public string lobby_id = string.Empty;
            public string host_account_id = string.Empty;
            public MapState map = null!;
            public PlayerState[] players = Array.Empty<PlayerState>();
        }
        [Serializable] private sealed class MapState
        {
            public string map_id = string.Empty;
            public int monster_hp;
            public string[] penned_mupo_ids = Array.Empty<string>();
            public bool mupo_failed;
        }
        [Serializable] private sealed class PlayerState
        {
            public string account_id = string.Empty;
            public string actor_id = string.Empty;
            public float[] position = Array.Empty<float>();
            public int experience;
            public int potions;
            public float resource;
        }

        private readonly ConcurrentQueue<Action> mainThread = new();
        private string host = "127.0.0.1";
        private string account = "player.one";
        private string joinCode = string.Empty;
        private string token = string.Empty;
        private string lobbyId = string.Empty;
        private string actorId = string.Empty;
        private string status = "Offline";
        private int sequence;
        private bool inFlight;
        private float nextSync;
        private GameObject? remotePlayer;
        [SerializeField] private string mapId = "map.training_ground";

        public void ConfigureMap(string selectedMapId) => mapId = selectedMapId;

        public void SendMupoPen(string mupoId)
        {
            if (mapId == "map.m102_mupo_round_up" && !string.IsNullOrEmpty(token))
                Send(new Request { kind = "herd_pen", token = token, lobby_id = lobbyId, mupo_id = mupoId, sequence = ++sequence });
        }

        public void SendMupoDeath(string mupoId)
        {
            if (mapId == "map.m102_mupo_round_up" && !string.IsNullOrEmpty(token))
                Send(new Request { kind = "herd_death", token = token, lobby_id = lobbyId, mupo_id = mupoId, sequence = ++sequence });
        }

        private void Update()
        {
            while (mainThread.TryDequeue(out var action)) action();
            if (string.IsNullOrEmpty(lobbyId) || inFlight || Time.unscaledTime < nextSync) return;
            nextSync = Time.unscaledTime + 0.15f;
            var keyboard = Keyboard.current;
            var direction = keyboard == null ? Vector3.zero : new Vector3(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), 0,
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)).normalized;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) SendSkill("skill.basic_slash");
            else if (keyboard != null && keyboard.digit2Key.wasPressedThisFrame) SendSkill("skill.power_strike");
            else if (keyboard != null && keyboard.digit3Key.wasPressedThisFrame) SendSkill("skill.class_special");
            else if (keyboard != null && keyboard.digit4Key.wasPressedThisFrame) SendSkill("skill.mole_stun_grenade");
            else if (keyboard != null && keyboard.digit5Key.wasPressedThisFrame) SendSkill("skill.blade_fang");
            else if (direction.sqrMagnitude > 0)
                Send(new Request { kind = "move", token = token, sequence = ++sequence, direction = new[] { direction.x, direction.y, direction.z } });
            else Send(new Request { kind = "state", token = token, lobby_id = lobbyId });
        }

        private void SendSkill(string inputSkillId)
        {
            var selector = GetComponent<CharacterSelector>();
            var skills = GetComponent<SkillExecutor>();
            var characterId = selector == null ? "wolf" : selector.SelectedId;
            if (inputSkillId == "skill.mole_stun_grenade" && characterId != "mole") return;
            if (inputSkillId == "skill.blade_fang" && characterId != "wolf") return;
            Send(new Request
            {
                kind = "action_request",
                token = token,
                actor_id = actorId,
                character_id = characterId,
                skill_id = skills == null ? inputSkillId : skills.ResolveSkillId(inputSkillId),
                target_id = "monster.training_dummy",
                aim = new[] { transform.forward.x, transform.forward.y, transform.forward.z },
                sequence = ++sequence
            });
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 330, 20, 310, 235), GUI.skin.box);
            GUILayout.Label("LAN Authoritative Server");
            host = GUILayout.TextField(host);
            account = GUILayout.TextField(account);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Login")) Send(new Request { kind = "login", account_id = account });
            if (GUILayout.Button("Create Lobby") && !string.IsNullOrEmpty(token)) Send(new Request { kind = "create_lobby", token = token, map_id = mapId });
            GUILayout.EndHorizontal();
            joinCode = GUILayout.TextField(joinCode);
            if (GUILayout.Button("Join Lobby") && !string.IsNullOrEmpty(token))
                Send(new Request { kind = "join_lobby", token = token, lobby_id = joinCode });
            GUILayout.Label($"Status: {status}\nLobby code: {lobbyId}");
            GUILayout.EndArea();
        }

        private void Send(Request request)
        {
            if (inFlight) return;
            inFlight = true;
            Task.Run(async () =>
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(host, 12712);
                    using var stream = client.GetStream();
                    var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request) + "\n");
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var json = await reader.ReadLineAsync();
                    var response = JsonUtility.FromJson<Response>(json);
                    mainThread.Enqueue(() => Apply(response));
                }
                catch (Exception error) { mainThread.Enqueue(() => status = error.Message); }
                finally { mainThread.Enqueue(() => inFlight = false); }
            });
        }

        private void Apply(Response response)
        {
            if (!response.ok) { status = response.error; return; }
            if (!string.IsNullOrEmpty(response.result.token)) token = response.result.token;
            if (!string.IsNullOrEmpty(response.result.account_id)) status = $"Logged in: {response.result.account_id}";
            if (!string.IsNullOrEmpty(response.result.lobby_id))
            {
                lobbyId = response.result.lobby_id;
                joinCode = lobbyId;
                status = $"Online | Monster HP {response.result.map.monster_hp}";
                var localAttack = GetComponent<MeleeAttack>();
                if (localAttack != null) localAttack.enabled = false;
                var localSkills = GetComponent<SkillExecutor>();
                if (localSkills != null) localSkills.enabled = false;
                var enemy = FindFirstObjectByType<EnemyTarget>(FindObjectsInactive.Include);
                if (enemy != null) enemy.gameObject.SetActive(response.result.map.monster_hp > 0);
                ApplyPlayers(response.result.players);
                var mission = FindAnyObjectByType<MupoHerdMission>();
                if (mission != null && response.result.map.map_id == "map.m102_mupo_round_up")
                    mission.Restore(response.result.map.penned_mupo_ids, response.result.map.mupo_failed);
            }
        }

        private void ApplyPlayers(PlayerState[] players)
        {
            foreach (var player in players)
            {
                if (player.position.Length != 3) continue;
                var position = new Vector3(player.position[0], player.position[1] + 1f, player.position[2]);
                if (player.account_id == account)
                {
                    actorId = player.actor_id;
                    transform.position = position;
                    var progress = GetComponent<PlayerProgress>();
                    if (progress != null) progress.Restore(player.experience, player.potions, player.experience > 0);
                    var quest = GetComponent<QuestProgress>();
                    if (quest != null) quest.Restore(player.experience > 0);
                    var skills = GetComponent<SkillExecutor>();
                    if (skills != null) skills.ApplyAuthoritativeResource(player.resource);
                }
                else
                {
                    if (remotePlayer == null)
                    {
                        remotePlayer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        remotePlayer.name = "Remote Player";
                        remotePlayer.GetComponent<Renderer>().material.color = Color.cyan;
                    }
                    remotePlayer.transform.position = position;
                }
            }
        }
    }
}
