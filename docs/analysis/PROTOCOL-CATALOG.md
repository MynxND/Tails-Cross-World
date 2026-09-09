# Protocol catalog

Status: preliminary. Payloads are unknown until call-site metadata and controlled behavior evidence agree. Numeric codes must not be assigned semantics from guesses.

| Code | Direction | Observed envelope | Count in current log | Meaning | Confidence |
|---:|---|---|---:|---|---:|
| 274 | Client event send | Hashtable, reliable=`True`, channel/flag=`True` | 1 | Unknown | High for occurrence; none for semantics |
| 291 | Client event send | Hashtable, reliable=`True`, channel/flag=`True` | 2 | Unknown | High for occurrence; none for semantics |
| 52 | Unknown | Mentioned in baseline document only | 0 | Unknown | Unconfirmed |
| 61 | Unknown | Mentioned in baseline document only | 0 | Unknown | Unconfirmed |
| 63 | Unknown | Mentioned in baseline document only | 0 | Unknown | Unconfirmed |

Evidence source for 274 and 291: private `12TailsOnline_Data/output_log.txt`. The log is not committed because it exposes local runtime paths and player-state details. The committed table contains only the minimum sanitized observation.

Required fields for each future entry: code, message kind, direction, valid client state, payload field types, reliability/channel flags, resulting state transition, rejection behavior, evidence reference, and confidence.
