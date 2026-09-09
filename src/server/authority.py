"""Authoritative two-player map, combat, reward, and reconnect state."""

from __future__ import annotations

from dataclasses import asdict, dataclass, field
import math
import secrets
import time
from typing import Callable


class AuthorityError(ValueError):
    pass


@dataclass
class PlayerState:
    account_id: str
    actor_id: str
    position: list[float] = field(default_factory=lambda: [0.0, 0.0, 0.0])
    hp: int = 100
    experience: int = 0
    potions: int = 0
    quest_complete: bool = False
    last_sequence: int = -1
    last_attack_at: float = -10.0


@dataclass
class Session:
    token: str
    account_id: str
    connected: bool = True
    disconnected_at: float | None = None


@dataclass
class MapInstance:
    instance_id: str
    monster_hp: int = 30
    monster_position: list[float] = field(default_factory=lambda: [0.0, 0.0, 3.0])


@dataclass
class Lobby:
    lobby_id: str
    host_account_id: str
    capacity: int = 2
    members: list[str] = field(default_factory=list)
    map: MapInstance = field(default_factory=lambda: MapInstance(secrets.token_hex(6)))


class Authority:
    MOVEMENT_STEP = 0.25
    ATTACK_RANGE = 2.25
    ATTACK_COOLDOWN = 0.35
    ATTACK_DAMAGE = 10

    def __init__(self, clock: Callable[[], float] = time.monotonic) -> None:
        self._clock = clock
        self._sessions: dict[str, Session] = {}
        self._players: dict[str, PlayerState] = {}
        self._lobbies: dict[str, Lobby] = {}

    def create_session(self, account_id: str) -> Session:
        if not account_id or len(account_id) > 64:
            raise AuthorityError("invalid account_id")
        session = Session(secrets.token_urlsafe(32), account_id)
        self._sessions[session.token] = session
        self._players.setdefault(account_id, PlayerState(account_id, f"actor.{account_id}"))
        return session

    def resume_session(self, token: str) -> Session:
        session = self.require_session(token, allow_disconnected=True)
        session.connected = True
        session.disconnected_at = None
        return session

    def disconnect(self, token: str) -> None:
        session = self.require_session(token)
        session.connected = False
        session.disconnected_at = self._clock()

    def require_session(self, token: str, allow_disconnected: bool = False) -> Session:
        session = self._sessions.get(token)
        if session is None or (not allow_disconnected and not session.connected):
            raise AuthorityError("invalid session")
        return session

    def create_lobby(self, token: str) -> Lobby:
        account_id = self.require_session(token).account_id
        if self._find_lobby(account_id):
            raise AuthorityError("account is already in a lobby")
        lobby = Lobby(secrets.token_hex(3), account_id, members=[account_id])
        self._lobbies[lobby.lobby_id] = lobby
        return lobby

    def join_lobby(self, token: str, lobby_id: str) -> Lobby:
        account_id = self.require_session(token).account_id
        lobby = self._require_lobby(lobby_id)
        if account_id in lobby.members:
            return lobby
        if self._find_lobby(account_id):
            raise AuthorityError("account is already in a lobby")
        if len(lobby.members) >= lobby.capacity:
            raise AuthorityError("lobby is full")
        lobby.members.append(account_id)
        return lobby

    def leave_lobby(self, token: str, lobby_id: str) -> None:
        account_id = self.require_session(token).account_id
        lobby = self._require_lobby(lobby_id)
        if account_id not in lobby.members:
            raise AuthorityError("account is not in this lobby")
        lobby.members.remove(account_id)
        if not lobby.members:
            del self._lobbies[lobby_id]
        elif lobby.host_account_id == account_id:
            lobby.host_account_id = lobby.members[0]

    def move(self, token: str, sequence: int, direction: list[float]) -> dict:
        player, lobby = self._context(token, sequence)
        if len(direction) != 3 or any(isinstance(x, bool) or not isinstance(x, (int, float)) or not math.isfinite(x) for x in direction):
            raise AuthorityError("invalid direction")
        magnitude = math.sqrt(sum(float(x) ** 2 for x in direction))
        if magnitude > 1.001:
            raise AuthorityError("direction exceeds unit length")
        player.position = [round(player.position[i] + float(direction[i]) * self.MOVEMENT_STEP, 4) for i in range(3)]
        return self.snapshot(lobby.lobby_id)

    def attack(self, token: str, sequence: int) -> dict:
        player, lobby = self._context(token, sequence)
        now = self._clock()
        if now - player.last_attack_at < self.ATTACK_COOLDOWN:
            raise AuthorityError("attack cooldown")
        if math.dist(player.position, lobby.map.monster_position) > self.ATTACK_RANGE:
            raise AuthorityError("target out of range")
        player.last_attack_at = now
        if lobby.map.monster_hp <= 0:
            raise AuthorityError("target already defeated")
        lobby.map.monster_hp = max(0, lobby.map.monster_hp - self.ATTACK_DAMAGE)
        if lobby.map.monster_hp == 0:
            for account_id in lobby.members:
                member = self._players[account_id]
                if not member.quest_complete:
                    member.quest_complete = True
                    member.experience += 25
                    member.potions += 1
        return self.snapshot(lobby.lobby_id)

    def snapshot(self, lobby_id: str) -> dict:
        lobby = self._require_lobby(lobby_id)
        return {"lobby_id": lobby.lobby_id, "host_account_id": lobby.host_account_id,
                "map": asdict(lobby.map), "players": [asdict(self._players[x]) for x in lobby.members]}

    def export_state(self) -> dict:
        return {"schema_version": 1, "players": [{
            "account_id": x.account_id, "actor_id": x.actor_id, "position": x.position,
            "hp": x.hp, "experience": x.experience, "potions": x.potions,
            "quest_complete": x.quest_complete,
        } for x in self._players.values()]}

    def import_state(self, state: dict) -> None:
        if state.get("schema_version") != 1 or not isinstance(state.get("players"), list):
            raise AuthorityError("unsupported persistence schema")
        players: dict[str, PlayerState] = {}
        for raw in state["players"]:
            player = PlayerState(**raw)
            if player.hp < 0 or player.experience < 0 or player.potions < 0:
                raise AuthorityError("invalid persisted player")
            players[player.account_id] = player
        self._players = players

    def _context(self, token: str, sequence: int) -> tuple[PlayerState, Lobby]:
        session = self.require_session(token)
        player = self._players[session.account_id]
        lobby = self._find_lobby(session.account_id)
        if lobby is None:
            raise AuthorityError("account is not in a lobby")
        if isinstance(sequence, bool) or not isinstance(sequence, int) or sequence <= player.last_sequence:
            raise AuthorityError("sequence must increase")
        player.last_sequence = sequence
        return player, lobby

    def _find_lobby(self, account_id: str) -> Lobby | None:
        return next((x for x in self._lobbies.values() if account_id in x.members), None)

    def _require_lobby(self, lobby_id: str) -> Lobby:
        try:
            return self._lobbies[lobby_id]
        except KeyError as error:
            raise AuthorityError("lobby not found") from error
