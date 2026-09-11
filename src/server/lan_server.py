"""Newline-delimited JSON LAN transport for the authoritative server."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import socketserver
from threading import Lock
from typing import Any
from .authority import Authority, AuthorityError
from .persistence import StateStore


class GameService:
    def __init__(self, store: StateStore | None = None) -> None:
        self.authority = Authority()
        self.store = store
        self.lock = Lock()
        if store and (state := store.load()):
            self.authority.import_state(state)

    def dispatch(self, request: Any) -> dict:
        if not isinstance(request, dict) or request.get("protocol_version") != 1 or not isinstance(request.get("kind"), str):
            raise AuthorityError("invalid request envelope")
        kind = request["kind"]
        with self.lock:
            if kind == "login":
                session = self.authority.create_session(request.get("account_id", ""))
                result = {"token": session.token, "account_id": session.account_id}
            elif kind == "resume":
                session = self.authority.resume_session(request.get("token", ""))
                result = {"token": session.token, "account_id": session.account_id}
            elif kind == "create_lobby":
                lobby = self.authority.create_lobby(request.get("token", ""), request.get("map_id", "map.training_ground"))
                result = self.authority.snapshot(lobby.lobby_id)
            elif kind == "join_lobby":
                lobby = self.authority.join_lobby(request.get("token", ""), request.get("lobby_id", ""))
                result = self.authority.snapshot(lobby.lobby_id)
            elif kind == "state":
                self.authority.require_session(request.get("token", ""))
                result = self.authority.snapshot(request.get("lobby_id", ""))
            elif kind == "move":
                result = self.authority.move(request.get("token", ""), request.get("sequence"), request.get("direction"))
            elif kind == "attack":
                result = self.authority.attack(request.get("token", ""), request.get("sequence"))
            elif kind == "action_request":
                result = self.authority.skill_action(
                    request.get("token", ""), request.get("sequence"), request.get("actor_id", ""),
                    request.get("skill_id", ""), request.get("target_id", ""), request.get("aim"))
            elif kind == "herd_pen":
                result = self.authority.herd_pen(request.get("token", ""), request.get("sequence"), request.get("mupo_id", ""))
            elif kind == "herd_death":
                result = self.authority.herd_death(request.get("token", ""), request.get("sequence"), request.get("mupo_id", ""))
            else:
                raise AuthorityError("unsupported request kind")
            if self.store:
                self.store.save(self.authority.export_state())
            return {"protocol_version": 1, "ok": True, "result": result}


class RequestHandler(socketserver.StreamRequestHandler):
    def handle(self) -> None:
        while line := self.rfile.readline(65537):
            try:
                if len(line) > 65536:
                    raise AuthorityError("request too large")
                response = self.server.service.dispatch(json.loads(line))  # type: ignore[attr-defined]
            except (AuthorityError, json.JSONDecodeError, TypeError) as error:
                response = {"protocol_version": 1, "ok": False, "error": str(error)}
            self.wfile.write(json.dumps(response, separators=(",", ":")).encode() + b"\n")


class LanServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True

    def __init__(self, address: tuple[str, int], service: GameService) -> None:
        self.service = service
        super().__init__(address, RequestHandler)


def main() -> None:
    parser = argparse.ArgumentParser(description="12 Tails LAN authoritative server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", default=12712, type=int)
    parser.add_argument("--data", type=Path, default=Path("server-data/state.json"))
    args = parser.parse_args()
    with LanServer((args.host, args.port), GameService(StateStore(args.data))) as server:
        print(f"12 Tails LAN server listening on {args.host}:{args.port}", flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()
