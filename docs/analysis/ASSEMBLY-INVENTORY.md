# Sanitized assembly inventory

Generated from CLI metadata only. The private full report contains names, methods, and fields and remains outside Git. No method bodies or decompiler output are included here.

| Assembly | Types | Role indicated by metadata |
|---|---:|---|
| Assembly-UnityScript.dll | 8,694 | Primary gameplay and content implementation |
| Assembly-CSharp.dll | 12 | Small C# gameplay/support layer |
| Assembly-CSharp-firstpass.dll | 48 | Early-compiled C# plugins/support |
| Assembly-UnityScript-firstpass.dll | 21 | Early-compiled UnityScript plugins/support |
| PhotonUnity3D.dll | 56 | Photon client transport |
| UnityEngine.dll | 401 | Legacy Unity runtime API |
| UnityScript.Lang.dll | 16 | UnityScript runtime support |
| Boo.Lang.dll | 102 | Boo/UnityScript language support |
| Mono.Security.dll | 221 | Legacy Mono security library |
| System.dll | 850 | Legacy .NET framework library |
| mscorlib.dll | 1,806 | Legacy Mono core library |

The primary gameplay assembly contains direct metadata evidence for `LoginGui`, `eLoginState`, `CharacterData`, `CharacterControl`, `PlayerData`, `MissionClass`, `MissionData`, `QuestClass`, `QuestData`, `SpawnZone`, shop/recipe types, character-specific skill types, and many RPC-named generated types. These names establish system presence but do not establish payload semantics or server authority.

Observed keyword groups in the game and Photon assemblies include 30 mission/quest type names, 67 combat/skill names, 20 shop/craft names, and 1,613 RPC/network-related names. Counts are discovery aids and may include compiler-generated or false-positive names.
