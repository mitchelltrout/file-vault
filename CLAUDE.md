
<!-- pdash:contract -->
## Project tracking (pdash)

This project is tracked by the project dashboard via `.dashboard/board.json`.
Keep it current using the `pdash` CLI — never hand-edit the JSON.

**On resume:** run `pdash list` first. Treat open cards + `nextStep` as the
standing backlog; reconcile with the session prompt (the prompt may add or
reprioritize, but the board is the memory of what remains).

**During planning:** break the plan into cards with `pdash add` *before*
implementing, so the plan is visible in the dashboard, not trapped in chat.

**During implementation:** `pdash move <id> doing` when starting a card,
`pdash done <id>` when finished. Log discovered bugs/ideas with `pdash add`.
Update `pdash next "<one line>"` to record where you left off.

**Bugs:** report → `pdash add "<title>" -t bug`; on fix **and verification** →
`pdash done <id>`. Closing a bug card asserts it is actually resolved.

**Manual steps** you cannot do yourself → `pdash add "<title>" -o me` so they
land in Mitchell's "Waiting on you" view.

**Operate-instructions:** when install/run steps change, update
`pdash run --install "..." --start "..."` and `pdash describe "..."`.
