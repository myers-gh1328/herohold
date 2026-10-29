# Explicit ability score generation

`AbilityGenerationRules` is a pure native module with one explicitly invoked randomness boundary. It matches the legacy `rules/ability_generation.py` standard array and four-d6-drop-lowest rule. No point-buy feature is added.

- `StandardArray`: read-only `[15,14,13,12,10,8]`.
- `Roll(Func<int>? rollD6 = null)`: makes exactly 24 d6 calls, drops exactly one lowest die from each four, and returns six scores sorted descending. An injected die makes tests deterministic. The default uses `RandomNumberGenerator.GetInt32(1,7)`.
- `ResolveAssignments(pool, assignments)`: requires six scores3–18 and every ability assigned a distinct pool index0–5. Equal values remain separate assignable positions.
- `Generate(source, AbilityGenerationMethod.StandardArray | Roll4d6, optionalDie)`: detached explicit reset/roll. Sets canonical method, identity positional assignment, and base scores. Roll also replaces `rolled_ability_scores`; standard array preserves the unused roll history. Other canonical state is preserved.
- `Swap(source, ability, position)`: detached positional swap with the current occupant; applies assigned base scores without any RNG call.
- `Validate(source)`: manual imports are unchanged. Generated methods require a valid pool/permutation and base scores matching it. Errors have stable codes `missing_ability_roll`, `invalid_ability_assignments`, `ability_assignment_mismatch`, and `invalid_ability_method`.

Canonical fields and legacy method names remain `ability_score_method` (`Manual entry`, `Standard array`, `Roll 4d6`), `rolled_ability_scores`, `ability_score_assignments` (ability name to index), and `base_ability_scores`. The app's fixed generate/assign operations choose the method or position; they do not accept a client-supplied score pool. Navigation and recalculation never generate scores. Selecting the roll method without rolling is an incomplete draft. Switching to manual entry permits direct scores again.

Tests use independent expected dice sequences, count exact die calls, reject impossible die values/duplicate slots, verify duplicate-valued pools and swapping, preserve original source/import evidence/current HP, and prove generated-method base scores cannot bypass the pool. Initial five stub failures precede implementation; creation's missing-pool validation has its own regression.
