# Explicit health and own-turn actions

`HealthChangeRequest : PlayRequest` carries `Kind` (Damage, Healing or TemporaryHp)
and an amount. `PlayRules.ChangeHealth` stages a detached change and records one
undo checkpoint using the same source identity, revision and idempotency guards
as casting and resource actions. `WorkspaceSession.ChangeHealthAsync` persists it.

Damage consumes temporary HP first and reduces current HP with a floor of zero.
Enter damage after table-adjudicated resistance/vulnerability; this is not an enemy
or incoming-damage rules engine. Healing leaves temporary HP alone and stops at
maximum HP. TemporaryHp explicitly replaces the pool; values never add together,
and zero clears the pool. The dialog says replacement before confirmation.

Amounts are bounded to 100000 and positive except explicit temporary-HP clearing.
Reaching zero HP clears concentration and its marked target through the existing
HP boundary. Undo restores current/temporary HP, concentration and target together,
while preserving unrelated later journal entries. Death saves and related table
outcomes are not automatically adjudicated.

`AdvanceOwnTurnRequest : PlayRequest` and `PlayRules.AdvanceOwnTurn` wrap the existing
turn update in an undoable guarded transaction. They increment the player's turn
counter, clear once-per-turn use and reduce manually counted effect durations by
one. They do not advance monsters or maintain an initiative order. The UI names
this action “Advance my turn”, explains it and names effects due to expire.

The new `HealthDialog.OpenAsync(kind, initialAmount = null)` only stages a review.
An optional healing amount from a spell result is a prefill; applying it requires
an explicit confirmation and does not cast/pay for the spell again.

The parameterized health regression covers damage absorbed/partially absorbed,
zero-HP damage, capped healing and replacement/clearing of temporary HP. Additional
cases cover concentration+undo, retries, invalid/stale input and guarded turn expiry.
The first eleven domain cases failed on the unimplemented seam, then passed.

Primary rules: [2024 damage/healing and temporary HP](https://www.dndbeyond.com/sources/dnd/br-2024/playing-the-game#TemporaryHitPoints).
