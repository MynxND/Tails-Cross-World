# System catalog

Status: Phase 1 working document. Claims below are hypotheses from filenames, class names, and runtime log wording. They are not recovered source behavior. Each row must gain direct evidence and a confidence rating before Phase 1 exits.

| System | Initial evidence | Current confidence | Next evidence needed |
|---|---|---:|---|
| Bootstrap and login | `LoginGui`, `Login_BG`, `eLoginState`, `serverDown` names reported in the baseline | Medium | Declaring types, state fields, and transition methods |
| Photon transport | `PhotonUnity3D.dll`; `PhotonPeer Created!` in local log | High | Operation/event call sites and payload shapes |
| Lobby and room | `lobby`, `serverSelect`, `serverEnter` names reported in the baseline | Medium | State transitions and actor creation call sites |
| Character | `CharacterData`; character initialization log entries | Medium | Fields, serialization boundaries, and stat mutations |
| Inventory | `InventoryList`, `InventoryKey`, item command names | Medium | Slot rules and every server-sensitive mutation |
| Guild | `GuildData`; guild hash loading log entry | Medium | Persistence format boundary and network ownership |
| Mission | Mission initialization, IDs, completion/failure wording | Medium | Mission state machine and reward authority |
| Combat and skill | RPC, effect, status, revive, KO, and damage names | Low | RPC catalog, validation rules, and damage state transitions |
| Monster and spawn | Resource loading, entity, spawn, and team names | Medium | Spawn inputs, AI ownership, and drop decisions |
| Map and portal | 271 level files and map/teleport names | High for content presence | Map identifiers, portal graph, and transition rules |
| Save and load | `.zrData` files under `SaveGame` | High for persistence presence | Read-only format identification and malformed-input tests |
| Shop and craft | Recipe, item, equipment, and upgrade names | Low | Transaction flow and authoritative validation |

Raw assembly inventory is generated privately and is not committed. Sanitized summaries and synthetic fixtures may be added after review.
