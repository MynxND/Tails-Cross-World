# Project Guidelines

## Continuity

- Read `docs/HANDOFF.md` before continuing implementation and update it after a completed phase or batch.
- Follow `docs/SKILL_BATCH_WORKFLOW.md` for character-skill work. Process all 12 characters as batches; do not default to one prompt, build, handoff update, or commit per skill.
- Reuse existing evidence artifacts and canonical content. Do not repeat legacy-source audits unless a required field is missing, contradictory, or marked `unknown`.

## Source And Authority

- Treat the original installation and AssetRipper export as read-only evidence. Never execute legacy scripts.
- Keep imported production assets under ignored `client/Assets/TwelveTails/LegacyPrivate/` and generated audit evidence under ignored `artifacts/`.
- Distinguish `proven`, `conditional`, `unknown`, and `prototype` values. Do not silently convert names or assumptions into gameplay behavior.
- The Python server owns combat outcomes, resources, rewards, and character-skill authorization. Unity clients send intent only while online.

## Validation And Commits

- Prefer table-driven schema, authority, and runtime-primitive tests over one test file or implementation path per skill.
- Run focused checks while developing, then one full repository suite, one full Unity gameplay suite, and one Windows build per completed batch.
- Commit coherent batch boundaries such as evidence/catalog, runtime primitives, or generated integration. Avoid per-skill commits unless the skill introduces an independently risky runtime primitive.
- Preserve unrelated user changes and exclude Unity-generated file-ID noise from commits. `ChapterMenu.unity` commonly changes only because of regeneration.
