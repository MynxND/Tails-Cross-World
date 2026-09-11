from __future__ import annotations

import json
from pathlib import Path
import socket
import tempfile
from threading import Thread
import unittest

from src.server.authority import Authority, AuthorityError
from src.server.lan_server import GameService, LanServer
from src.server.persistence import StateStore


class ServerAuthorityTests(unittest.TestCase):
    def test_two_authenticated_players_can_join(self) -> None:
        authority = Authority()
        first = authority.create_session("account.first")
        second = authority.create_session("account.second")
        lobby = authority.create_lobby(first.token)

        joined = authority.join_lobby(second.token, lobby.lobby_id)

        self.assertEqual(joined.members, ["account.first", "account.second"])

    def test_invalid_session_and_third_player_are_rejected(self) -> None:
        authority = Authority()
        first = authority.create_session("account.first")
        second = authority.create_session("account.second")
        third = authority.create_session("account.third")
        lobby = authority.create_lobby(first.token)
        authority.join_lobby(second.token, lobby.lobby_id)

        with self.assertRaisesRegex(AuthorityError, "invalid session"):
            authority.join_lobby("forged", lobby.lobby_id)
        with self.assertRaisesRegex(AuthorityError, "full"):
            authority.join_lobby(third.token, lobby.lobby_id)

    def test_host_migrates_and_empty_lobby_is_removed(self) -> None:
        authority = Authority()
        first = authority.create_session("account.first")
        second = authority.create_session("account.second")
        lobby = authority.create_lobby(first.token)
        authority.join_lobby(second.token, lobby.lobby_id)

        authority.leave_lobby(first.token, lobby.lobby_id)
        self.assertEqual(lobby.host_account_id, "account.second")
        authority.leave_lobby(second.token, lobby.lobby_id)
        with self.assertRaisesRegex(AuthorityError, "not found"):
            authority.join_lobby(first.token, lobby.lobby_id)

    def test_server_owns_movement_combat_and_rewards(self) -> None:
        now = [1.0]
        authority = Authority(lambda: now[0])
        first = authority.create_session("first")
        second = authority.create_session("second")
        lobby = authority.create_lobby(first.token)
        authority.join_lobby(second.token, lobby.lobby_id)
        for sequence in range(1, 5):
            authority.move(first.token, sequence, [0, 0, 1])
        with self.assertRaisesRegex(AuthorityError, "sequence"):
            authority.move(first.token, 4, [0, 0, 1])
        with self.assertRaisesRegex(AuthorityError, "unit length"):
            authority.move(first.token, 5, [9, 0, 0])
        for sequence in range(6, 9):
            now[0] += 1
            state = authority.attack(first.token, sequence)
        self.assertEqual(state["map"]["monster_hp"], 0)
        self.assertTrue(all(x["experience"] == 25 and x["potions"] == 1 for x in state["players"]))

    def test_server_validates_skill_actor_cooldown_resource_and_range(self) -> None:
        now = [10.0]
        authority = Authority(lambda: now[0])
        session = authority.create_session("skilled")
        lobby = authority.create_lobby(session.token)
        player = authority._players["skilled"]
        player.position = [0.0, 0.0, 1.0]

        with self.assertRaisesRegex(AuthorityError, "owned"):
            authority.skill_action(session.token, 1, "actor.other", "skill.power_strike", "monster.training_dummy", [0, 0, 1])
        with self.assertRaisesRegex(AuthorityError, "unknown skill"):
            authority.skill_action(session.token, 2, player.actor_id, "skill.missing", "monster.training_dummy", [0, 0, 1])

        state = authority.skill_action(session.token, 3, player.actor_id, "skill.power_strike", "monster.training_dummy", [0, 0, 1])
        self.assertEqual(state["map"]["monster_hp"], 10)
        self.assertEqual(state["players"][0]["resource"], 95.0)
        with self.assertRaisesRegex(AuthorityError, "cooldown"):
            authority.skill_action(session.token, 4, player.actor_id, "skill.power_strike", "monster.training_dummy", [0, 0, 1])

        player.resource = 0.0
        now[0] += 1.2
        state = authority.skill_action(session.token, 5, player.actor_id, "skill.power_strike", "monster.training_dummy", [0, 0, 1])
        self.assertAlmostEqual(state["players"][0]["resource"], 1.0)

        lobby.map.monster_hp = 30
        player.position = [0.0, 0.0, 0.0]
        now[0] += 2.0
        with self.assertRaisesRegex(AuthorityError, "range"):
            authority.skill_action(session.token, 6, player.actor_id, "skill.power_strike", "monster.training_dummy", [0, 0, 1])

    def test_tcp_action_request_uses_authoritative_skill_values(self) -> None:
        with LanServer(("127.0.0.1", 0), GameService()) as server:
            thread = Thread(target=server.serve_forever, daemon=True)
            thread.start()
            with socket.create_connection(server.server_address) as connection:
                stream = connection.makefile("rwb")

                def request(kind, **values):
                    payload = {"protocol_version": 1, "kind": kind, **values}
                    stream.write(json.dumps(payload).encode() + b"\n")
                    stream.flush()
                    return json.loads(stream.readline())

                login = request("login", account_id="skill-player")["result"]
                lobby = request("create_lobby", token=login["token"])["result"]
                actor_id = lobby["players"][0]["actor_id"]
                response = request(
                    "action_request", token=login["token"], sequence=1, actor_id=actor_id,
                    skill_id="skill.class_special", target_id="monster.training_dummy", aim=[0, 0, 1])
                self.assertTrue(response["ok"])
                self.assertEqual(response["result"]["map"]["monster_hp"], 0)
                self.assertEqual(response["result"]["players"][0]["resource"], 85.0)
            server.shutdown()

    def test_sheep_class_special_is_restricted_to_sheep(self) -> None:
        authority = Authority()
        session = authority.create_session("class-skill")
        lobby = authority.create_lobby(session.token)
        player = authority._players["class-skill"]

        with self.assertRaisesRegex(AuthorityError, "unavailable for character"):
            authority.skill_action(session.token, 1, player.actor_id, "skill.sheep_class_special", "monster.training_dummy", [0, 0, 1], "wolf")

        state = authority.skill_action(session.token, 2, player.actor_id, "skill.sheep_class_special", "monster.training_dummy", [0, 0, 1], "sheep")
        self.assertEqual(state["map"]["monster_hp"], 0)
        self.assertEqual(state["players"][0]["character_id"], "sheep")

    def test_mole_grenade_is_restricted_to_mole(self) -> None:
        authority = Authority()
        session = authority.create_session("grenadier")
        lobby = authority.create_lobby(session.token)
        player = authority._players["grenadier"]

        with self.assertRaisesRegex(AuthorityError, "unavailable for character"):
            authority.skill_action(session.token, 1, player.actor_id, "skill.mole_stun_grenade", "monster.training_dummy", [0, 0, 1], "sheep")

        state = authority.skill_action(session.token, 2, player.actor_id, "skill.mole_stun_grenade", "monster.training_dummy", [0, 0, 1], "mole")
        self.assertEqual(state["map"]["monster_hp"], 20)
        self.assertEqual(state["players"][0]["character_id"], "mole")

    def test_wolf_blade_fang_is_restricted_and_applies_both_hits(self) -> None:
        authority = Authority()
        session = authority.create_session("blade-fang")
        lobby = authority.create_lobby(session.token)
        player = authority._players["blade-fang"]

        with self.assertRaisesRegex(AuthorityError, "unavailable for character"):
            authority.skill_action(session.token, 1, player.actor_id, "skill.wolf_blade_fang", "monster.training_dummy", [0, 0, 1], "mole")

        state = authority.skill_action(session.token, 2, player.actor_id, "skill.wolf_blade_fang", "monster.training_dummy", [0, 0, 1], "wolf")
        self.assertEqual(state["map"]["monster_hp"], 10)
        self.assertEqual(state["players"][0]["character_id"], "wolf")

    def test_panda_three_steps_is_restricted_and_applies_all_hits(self) -> None:
        authority = Authority()
        session = authority.create_session("three-steps")
        authority.create_lobby(session.token)
        player = authority._players["three-steps"]
        player.position = [0.0, 0.0, 1.0]

        with self.assertRaisesRegex(AuthorityError, "unavailable for character"):
            authority.skill_action(session.token, 1, player.actor_id, "skill.panda_three_steps", "monster.training_dummy", [0, 0, 1], "wolf")

        state = authority.skill_action(session.token, 2, player.actor_id, "skill.panda_three_steps", "monster.training_dummy", [0, 0, 1], "panda")
        self.assertEqual(state["map"]["monster_hp"], 0)
        self.assertEqual(state["players"][0]["character_id"], "panda")

    def test_persistence_and_reconnect(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            store = StateStore(Path(directory) / "state.json")
            authority = Authority()
            session = authority.create_session("saved")
            authority.disconnect(session.token)
            self.assertEqual(authority.resume_session(session.token).account_id, "saved")
            store.save(authority.export_state())
            restored = Authority()
            restored.import_state(store.load())
            self.assertEqual(restored.create_session("saved").account_id, "saved")

    def test_m102_herd_completion_is_authoritative_and_idempotent(self) -> None:
        authority = Authority()
        session = authority.create_session("herder")
        lobby = authority.create_lobby(session.token, "map.m102_mupo_round_up")
        for sequence in range(1, 7):
            state = authority.herd_pen(session.token, sequence, f"mupo-{sequence}")
        self.assertEqual(state["map"]["penned_mupo_ids"], [f"mupo-{index}" for index in range(1, 7)])
        self.assertTrue(state["players"][0]["quest_complete"])
        self.assertEqual(state["players"][0]["experience"], 50)
        with self.assertRaisesRegex(AuthorityError, "already penned"):
            authority.herd_pen(session.token, 7, "mupo-1")

    def test_m102_herd_death_fails_mission_without_reward(self) -> None:
        authority = Authority()
        session = authority.create_session("herder")
        authority.create_lobby(session.token, "map.m102_mupo_round_up")
        state = authority.herd_death(session.token, 1, "mupo-3")
        self.assertTrue(state["map"]["mupo_failed"])
        self.assertEqual(state["players"][0]["experience"], 0)
        with self.assertRaisesRegex(AuthorityError, "mission failed"):
            authority.herd_pen(session.token, 2, "mupo-1")

    def test_two_tcp_clients_complete_integration_flow(self) -> None:
        with LanServer(("127.0.0.1", 0), GameService()) as server:
            thread = Thread(target=server.serve_forever, daemon=True)
            thread.start()
            address = server.server_address

            def request(stream, kind, **values):
                payload = {"protocol_version": 1, "kind": kind, **values}
                stream.write(json.dumps(payload).encode() + b"\n")
                stream.flush()
                return json.loads(stream.readline())

            with socket.create_connection(address) as a, socket.create_connection(address) as b:
                af, bf = a.makefile("rwb"), b.makefile("rwb")
                one = request(af, "login", account_id="one")["result"]
                two = request(bf, "login", account_id="two")["result"]
                lobby = request(af, "create_lobby", token=one["token"])["result"]
                joined = request(bf, "join_lobby", token=two["token"], lobby_id=lobby["lobby_id"])
                self.assertEqual(len(joined["result"]["players"]), 2)
                rejected = request(bf, "move", token=two["token"], sequence=1, direction=[99, 0, 0])
                self.assertFalse(rejected["ok"])
            server.shutdown()

    def test_tcp_m102_herd_flow_is_server_authoritative(self) -> None:
        with LanServer(("127.0.0.1", 0), GameService()) as server:
            thread = Thread(target=server.serve_forever, daemon=True)
            thread.start()
            with socket.create_connection(server.server_address) as connection:
                stream = connection.makefile("rwb")

                def request(kind, **values):
                    payload = {"protocol_version": 1, "kind": kind, **values}
                    stream.write(json.dumps(payload).encode() + b"\n")
                    stream.flush()
                    return json.loads(stream.readline())

                login = request("login", account_id="m102-player")["result"]
                lobby = request("create_lobby", token=login["token"], map_id="map.m102_mupo_round_up")["result"]
                self.assertEqual(lobby["map"]["map_id"], "map.m102_mupo_round_up")
                for sequence in range(1, 7):
                    response = request("herd_pen", token=login["token"], lobby_id=lobby["lobby_id"], sequence=sequence, mupo_id=f"mupo-{sequence}")
                    self.assertTrue(response["ok"])
                self.assertTrue(response["result"]["players"][0]["quest_complete"])
                self.assertEqual(response["result"]["players"][0]["experience"], 50)
            server.shutdown()
