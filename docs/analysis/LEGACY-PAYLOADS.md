# Legacy event payload observations

This is a sanitized IL-derived field map. Numeric keys are legacy Hashtable keys. Names describe the value loaded at the call site; they do not define the new protocol.

| Event | Legacy keys and observed values | Notes |
|---:|---|---|
| 75 | 122-124 position x/y/z; 126-128 forward x/y/z | Values are rounded after scaling position by 50 and forward by 1,000 |
| 77 | 120 revive value; 101 hp; 102 mp; 103 sp; 104 ko; 153 source/argument; 157 additional value; 122-124 position; 126-128 forward | Client sends resulting state, so a compatible server must not trust it as authority |
| 78 | 101 hp; 102 mp; 103 sp; 104 ko | Direct client state synchronization is unsuitable for the new authoritative protocol |
| 91 | includes 154-156 force vector scaled by 1,000; 157 integer argument; 159 computed value | Damage method contains extensive client-side combat calculation |
| 92 | includes 154-156 force vector scaled by 1,000; 157 integer argument; 159 computed value | Effect-damage variant |
| 93 | 101 hp; 102 mp; 103 sp; 104 ko; 153 value; 157 integer argument; 159 computed value | Client sends resulting heal/state values |
| 94 | 21 status code; 22-24 signed 16-bit values; 25 integer value | Status mutation request/state relay |
| 174 | 21 source slot + 100; 22 destination slot + 100 | Observed in item-list swap and storage UI handlers |

The `SendEvent` call shape observed in IL is actor number, event code, Hashtable payload, and two Boolean flags. Event 174 passes actor number zero, a false first flag, and a true second flag; character events shown above use the current actor and two true flags.

## Clean-room authority decision

The new client sends intent only. It may identify an actor, selected skill, target, direction, or inventory slots. It may not choose resulting damage, healing, HP/MP/SP/KO values, status duration, drops, experience, currency, quest completion, or inventory item identity. The server calculates those results from server-owned state and definitions.
