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
