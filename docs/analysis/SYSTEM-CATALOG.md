# System catalog

Status: Phase 1 working document. Claims below are hypotheses from filenames, class names, and runtime log wording. They are not recovered source behavior. Each row must gain direct evidence and a confidence rating before Phase 1 exits.

| System | Initial evidence | Current confidence | Next evidence needed |
|---|---|---:|---|
| Bootstrap and login | Metadata confirms `LoginGui` and `eLoginState`; baseline also reports `Login_BG` and `serverDown` | High for presence | State fields and transition methods |
| Photon transport | Metadata confirms 56 types in `PhotonUnity3D.dll`; `PhotonPeer Created!` in local log | High | Operation/event call sites and payload shapes |
| Lobby and room | `lobby`, `serverSelect`, `serverEnter` names reported in the baseline | Medium | State transitions and actor creation call sites |
| Character | Metadata confirms `CharacterData`, `CharacterControl`, and `PlayerData`; character initialization log entries | High for presence | Fields, serialization boundaries, and stat mutations |
| Inventory | `InventoryList`, `InventoryKey`, item command names | Medium | Slot rules and every server-sensitive mutation |
| Guild | `GuildData`; guild hash loading log entry | Medium | Persistence format boundary and network ownership |
| Mission | Metadata confirms `MissionClass`, `MissionData`, `QuestClass`, and `QuestData` families | High for presence | Mission state machine and reward authority |
| Combat and skill | Metadata includes 67 combat/skill keyword matches and many RPC-generated names | Medium for presence | RPC catalog, validation rules, and damage state transitions |
| Monster and spawn | Metadata confirms `SpawnZone` and mission-specific monster types | Medium | Spawn inputs, AI ownership, and drop decisions |
| Map and portal | 271 level files and map/teleport names | High for content presence | Map identifiers, portal graph, and transition rules |
| Save and load | `.zrData` files under `SaveGame` | High for persistence presence | Read-only format identification and malformed-input tests |
| Shop and craft | Metadata confirms `ShopData`, `ShopGui`, `RecipeData`, pet and arena shop families | High for presence | Transaction flow and authoritative validation |

Raw assembly inventory is generated privately and is not committed. Sanitized summaries and synthetic fixtures may be added after review.
