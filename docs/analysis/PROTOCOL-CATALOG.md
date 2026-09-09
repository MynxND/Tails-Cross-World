# Protocol catalog

Status: preliminary. IL call-site evidence corrects an ambiguity in the baseline log: strings such as `274...` and `291...` are concatenated values, not confirmed three-digit opcodes. `SendEvent` call sites place event codes 74 and 91 immediately before the reliability flags. Payloads remain unknown until call-site structure and controlled behavior evidence agree.

| Event code | Direction | Representative call site | Calls found | Tentative meaning | Confidence |
|---:|---|---|---:|---|---:|
| 15 | Client send | Compiler-generated `MoveNext` | 1 | Unknown | Low |
| 73 | Client send | `Lavu.NetworkUpdate`, `Annonite.NetworkUpdate` | 2 | Entity network update | Medium |
| 74 | Client send | 211 `ActionEvent` methods across actors | 211 | Actor action | High |
| 75 | Client send | `CharacterControl.KoEvent` | 1 | Knockout | Medium-high |
| 77 | Client send | `CharacterControl.ReviveEvent` | 1 | Revive | Medium-high |
| 78 | Client send | `CharacterControl.HPMPSPKOEvent` | 1 | HP/MP/SP/KO state | Medium-high |
| 88 | Client send | `CharacterControl.RPC_createEffect` | 1 | Effect creation/relay | Medium |
| 91 | Client send | `CharacterControl.RPC_AddDamage` | 1 | Damage | Medium-high |
| 92 | Client send | `CharacterControl.RPC_AddEffectDamage` | 1 | Effect damage | Medium-high |
| 93 | Client send | `CharacterControl.RPC_AddHeal` | 1 | Healing | Medium-high |
| 94 | Client send | `CharacterControl.RPC_AddStatus` | 1 | Status addition | Medium-high |
| 174 | Client send | inventory/storage swap and render handlers | 3 | Inventory/storage synchronization | Medium |

Values 52, 61, and 63 remain unconfirmed baseline claims because they do not appear in the current runtime log and were not established by this targeted call-site pass. The private log and full IL call-site report are not committed because they expose local paths and implementation details. The committed table contains only a sanitized observation summary.

Required fields for each future entry: code, message kind, direction, valid client state, payload field types, reliability/channel flags, resulting state transition, rejection behavior, evidence reference, and confidence.
