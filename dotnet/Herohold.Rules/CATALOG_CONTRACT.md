# Native catalog snapshot

`RulesCatalogSnapshot.Parse(classesJson, speciesJson, backgroundsJson, featsJson, spellsJson, equipmentJson, weaponsJson, rawSpellsJson)` consumes trusted JSON strings once. The caller owns fixed repository/configuration paths; this API never reads player-supplied paths or executes catalog content.

The public snapshot exposes Classes, Species, Backgrounds, Feats, Spells and Weapons as IReadOnlyList<JsonObject>, and Equipment as IReadOnlyList<string>. Each getter returns a detached read-only collection containing detached mutable JSON records. Unknown record fields, JSON value types and array order survive. ToEnvelope() returns a new detached JsonObject with exactly `{catalogs:{classes,species,backgrounds,feats,spells,equipment,weapons}}`.

All seven normalized inputs are arrays. Equipment entries remain strings; the other six arrays contain objects. Raw spell input is an object whose optional spell property is an object-record array. Every normalized spell receives a boolean ritual value, replacing any existing flag, using a case-insensitive (name,source) raw lookup; missing normalized source defaults to XPHB, missing matches are false, and the last matching raw record wins. Raw meta.ritual uses Python boolean truthiness. Raw spell records are not text-cleaned before lookup.

Recursive cleanup touches string values containing `{@` only, never JSON property names. A single pass replaces innermost `\{@([^{}]+)\}` matches. The tag is case-insensitive; dc emits `DC <first-part>`, filter emits the first part, other tags use the third nonempty pipe part when available, otherwise the first part or empty body. The resulting tagged string is stripped using Python whitespace semantics, including U+001C–U+001F. Untagged strings retain surrounding whitespace. Nested outer tags remain after the single pass.

Bounds are 16 MiB per input, 32 MiB aggregate UTF-8 bytes, JSON depth 64, 250,000 aggregate JSON nodes and 50,000 records per array. Malformed JSON, unsupported shapes and excessive bounds raise ArgumentException. Catalog content is treated as data, including unknown fields.

`NativeCatalogService(RulesCatalogSnapshot)` provides `Task<JsonObject> LoadAsync(CancellationToken=default)`. It checks cancellation before and after detaching the envelope, shares no mutable results between callers, and requires no Python process. Program/WorkspaceSession cutover remains a separate integration gate.

Verification compares the entire seven-key result against the public Python catalog operation, plus independent source counts 12/10/16/55/391/217/41, ritual facts, recursive cleanup examples, collection order, detachment and bounded failure cases. No spell text becomes executable rules through this loader.

Program now supplies the fixed trusted file inputs once at startup. WorkspaceSession
routes the catalog operation to this service; an unavailable-Python spy regression
observed the old call fail and now verifies zero compatibility calls. Other builder
and document operations continue using their explicit transitional boundary.
