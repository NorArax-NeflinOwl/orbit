# Local model measurements — step 3 of the assistant plan

What a small model on a laptop actually costs, and whether it can do the one language job the
[assistant plan](ai-assistant-plan.md) is least sure about: correcting Polish. Measured, not assumed,
because §4 of that plan says to measure it before committing to the feature.

Measured on 30 August 2026, on an Apple Silicon Mac (arm64, 16 GB), against the `ollama` service now in
[`docker-compose.yml`](../docker-compose.yml) — Docker Desktop's Linux VM with 8 CPUs and 8 GB, and **no
GPU**, because Docker on macOS does not pass one through. That is the same CPU-only shape as the
"Ollama on Container Apps (CPU)" column of the plan's comparison table, so the latency below is a fair
reading of that column and a pessimistic one for a native install.

## Latency

`llama3.2:3b`, short replies (roughly 30–40 generated tokens), temperature 0:

| | Wall clock | Throughput |
| --- | --- | --- |
| First call after the container starts | 10.9 s (6.4 s of it loading the model into memory) | 9.1 tokens/s |
| Warm, five consecutive short replies | 3.7 s, 4.5 s, 4.5 s, 9.3 s, 14.3 s | 2.9–11.2 tokens/s |
| One inventory name in, one out (20 samples) | median 1.4 s, min 0.9 s, max 28.2 s | — |

Two things this says:

- **The round trip works and is fast enough to develop against.** A single short answer is seconds, not
  minutes, and the first-token cost after an idle container is about six seconds.
- **The variance is the real finding.** Identical prompts ranged from 3.7 s to 14.3 s, and the first
  call of a batch repeatedly cost 15–28 s. An earlier run of the same script, taken while the machine
  was busy with unrelated builds (load average ~280), fell to **0.2 tokens/s — 181 s for a 39-token
  reply**. Those numbers are excluded from the table above as unrepresentative, but they are worth
  keeping in view: a CPU-only model has no floor when it is sharing a machine. This is why the plan
  does not propose shipping one.

### Through Orbit's own client

The numbers above come from Ollama's HTTP API directly. The same question asked through
`AssistantChatClient` — `Microsoft.Extensions.AI` over the OpenAI-compatible route, which is the path
production will use — cost **40.6 s on the first call** (the container had gone idle and reloaded the
model) and **4.8 s and 6.3 s warm**, for a one-sentence answer. The client adds nothing measurable; the
model is the whole cost.

Worth recording alongside the correction results: those free-form answers were themselves malformed
Polish — "planetka w systemie szeregowej o orbitach przeciwskuperczerwonym" — and confidently invented,
since the model has been told nothing about Orbit. Generation quality at 3B is the same story as
correction quality.

## Polish correction: a 3B model does not pass

The decisive test. Twenty real names from an Orbit inventory — **twelve already correct, eight with a
genuine error** — each sent on its own with an instruction to fix spelling only and to return a correct
name unchanged. Scored against a ground truth: for the twelve, "unchanged" is the only right answer.

| Model / prompt | Correct names left alone | Real errors fixed |
| --- | --- | --- |
| `llama3.2:3b`, plain instruction | 6 / 12 | **0 / 8** |
| `llama3.2:3b`, few-shot instruction | 11 / 12 | **1 / 8** |
| `qwen2.5:3b`, few-shot instruction | 9 / 12 | **0 / 8** |

Not one of the three configurations corrected more than one of the eight real errors. What the models
did instead:

**It "fixes" what was already right** — the exact failure §4 predicts:

| Was | Became |
| --- | --- |
| Ogórki konserwowe | Ogórki w konserwie |
| Śledzie w śmietanie | Śledź w śmietanie |
| Makaron świderki | Makaron świdry |
| Kret w żelu | Kret w żelku |
| Gąbka pod prysznic | Gąbka pod prysznica |
| Bułeczki hamburgerowe | Bułki hamburgerowe |

Those are rewrites of grammatical number, case and vocabulary, not spelling corrections — and "Kret" is
a brand name the instruction told it to leave alone.

**It misses plain missing diacritics**, which is the single most common real error in Polish typing and
the thing a correction feature exists for:

| Was | Should be | Model said |
| --- | --- | --- |
| Mieso wołowe mielone | Mięso wołowe mielone | Mieso wołowe mielone |
| Reczniki papierowe | Ręczniki papierowe | Reczniki papierowe |
| Smietanka 30% | Śmietanka 30% | Smietanka 30% |
| Sok Grejfrutowy | Sok grejpfrutowy | Sok grejfrutowy |

**And on a real typo it invents a word that does not exist:**

| Was | Should be | Model said |
| --- | --- | --- |
| Włożczyzna | Włoszczyzna | Włoszczyna / Włóczyna |
| Worlki na śmieci średnie 35l | Worki na śmieci średnie 35l | Worłki … / Worzalki … |
| Worki na śmiecie małe < 15l | Worki na śmieci małe < 15l | Praca na śmieci małe < 15l |
| Sok Grejfrutowy | Sok grejpfrutowy | Sok Grężywowy |

A better prompt moves the failure rather than removing it: the few-shot version stopped rewriting
correct names (11/12 left alone) at the cost of fixing almost nothing (1/8). Between "confidently wrong"
and "silent", neither is a feature.

## What follows

- **Job 2 (check the Polish) does not ship on a 3B model**, with any prompt. Offering it would mean
  proposing "Ogórki w konserwie" over a name that was already correct, while leaving "Mieso" alone.
- **The same twenty names are the acceptance test for the hosted model**, before job 2 is enabled in
  production. The threshold that makes the feature worth having: it must leave all twelve correct names
  untouched *and* fix a clear majority of the eight. The script that produced this table is small enough
  to re-run against any OpenAI-compatible endpoint by changing the model name and base URL.
- **Whether the gap is model size or open-weight Polish generally is untested.** An ~8B model is the
  obvious next probe, but the plan's own comparison table already rules 8B out on latency for the CPU
  host, so it would settle a question of curiosity rather than a decision.
- **None of this changes what Ollama is for.** It is the development stand-in the plan describes: no
  key, no cost, no network, and fast enough to build the assistant against. Its language quality was
  never the reason it is in `docker-compose.yml`.
