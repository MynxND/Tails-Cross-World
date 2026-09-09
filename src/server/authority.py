"""In-memory session and lobby authority used by the LAN transport."""

from __future__ import annotations

from dataclasses import dataclass, field
import secrets


class AuthorityError(ValueError):
    pass


@dataclass(frozen=True)
class Session:
    token: str
    account_id: str


@dataclass
class Lobby:
    lobby_id: str
    host_account_id: str
    capacity: int = 2
    members: list[str] = field(default_factory=list)


class Authority:
    """Owns authenticated sessions and two-player lobby membership."""

    def __init__(self) -> None:
        self._sessions: dict[str, Session] = {}
        self._lobbies: dict[str, Lobby] = {}

    def create_session(self, account_id: str) -> Session:
        if not account_id or len(account_id) > 64:
            raise AuthorityError("invalid account_id")
        token = secrets.token_urlsafe(32)
        session = Session(token, account_id)
        self._sessions[token] = session
        return session

    def require_session(self, token: str) -> Session:
        try:
            return self._sessions[token]
        except KeyError as error:
            raise AuthorityError("invalid session") from error

    def create_lobby(self, token: str) -> Lobby:
        account_id = self.require_session(token).account_id
        if any(account_id in lobby.members for lobby in self._lobbies.values()):
            raise AuthorityError("account is already in a lobby")
        lobby_id = secrets.token_hex(6)
        lobby = Lobby(lobby_id, account_id, members=[account_id])
        self._lobbies[lobby_id] = lobby
        return lobby

    def join_lobby(self, token: str, lobby_id: str) -> Lobby:
        account_id = self.require_session(token).account_id
        try:
            lobby = self._lobbies[lobby_id]
        except KeyError as error:
            raise AuthorityError("lobby not found") from error
        if account_id in lobby.members:
            return lobby
        if any(account_id in candidate.members for candidate in self._lobbies.values()):
            raise AuthorityError("account is already in a lobby")
        if len(lobby.members) >= lobby.capacity:
            raise AuthorityError("lobby is full")
        lobby.members.append(account_id)
        return lobby

    def leave_lobby(self, token: str, lobby_id: str) -> None:
        account_id = self.require_session(token).account_id
        try:
            lobby = self._lobbies[lobby_id]
        except KeyError as error:
            raise AuthorityError("lobby not found") from error
        if account_id not in lobby.members:
            raise AuthorityError("account is not in this lobby")
        lobby.members.remove(account_id)
        if not lobby.members:
            del self._lobbies[lobby_id]
        elif lobby.host_account_id == account_id:
            lobby.host_account_id = lobby.members[0]

