from __future__ import annotations

import unittest

from src.server.authority import Authority, AuthorityError


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

