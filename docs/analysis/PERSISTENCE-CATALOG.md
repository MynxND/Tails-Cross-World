# Persistence catalog

The eight `.zrData` files in the current `SaveGame` directory begin with the standard .NET BinaryFormatter/NRBF serialized-stream header. This identifies the container family; it does not yet prove every object graph or field meaning.

| File family | Files observed | Size range | Format confidence |
|---|---:|---:|---:|
| CharacterData | 4 | 4,295-6,260 bytes | High |
| GuildData | 1 | 226 bytes | High |
| InventoryKey | 1 | 54 bytes | High |
| InventoryList | 1 | 17,346 bytes | High |
| RecipeList | 1 | 127 bytes | High |

BinaryFormatter data is never deserialized by Phase 1 tooling. Deserializing an untrusted BinaryFormatter stream can instantiate unexpected types and execute dangerous behavior. The current probe only reads bytes, checks the fixed NRBF header, calculates SHA-256 and entropy, and records file sizes. A future parser must decode an explicit allowlist of NRBF records into inert values and reject unknown records, excessive lengths, invalid references, and truncated input.

The private generated report remains outside Git and contains no decoded player fields.
