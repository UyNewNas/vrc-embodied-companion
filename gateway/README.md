# Reference transport gateway

This directory contains a **protocol reference**, not a production LLM service.

It exists to make `docs/LLM_TRANSPORT_RFC.md` executable before a VRChat `World/` project is available.

## What it proves

- the selected route is finite and stateless;
- only bounded route codes are accepted;
- unsupported languages and query parameters are rejected rather than silently widening the privacy surface;
- the server returns `BehaviorPlan v0.1`-shaped JSON;
- POST is rejected so the prototype does not accidentally drift into an API surface Udon does not currently expose;
- no cookie/session/user identifier is required;
- client addresses and route paths are not logged by the reference handler;
- invalid routes return bounded JSON errors rather than HTML.

It deliberately uses only Python's standard library.

## Run

```bash
cd gateway
python reference_server.py --host 127.0.0.1 --port 8787
```

Example:

```text
GET http://127.0.0.1:8787/v1/plan/default/quiet_company/warm/quiet/zh/2
```

A successful response is compatible with the current `BehaviorPlan v0.1` shape.

## Tests

```bash
cd gateway
python -m unittest -v test_reference_server.py
```

The stricter prototype was executed outside Unity on 2026-09-16 and all **9** reference tests passed. This is **not** evidence that UdonSharp or `VRCStringDownloader` integration works; that proof remains #18 and is blocked on the runnable #2 world.

## Production boundary

Do not deploy this file as-is as the public service. A production live gateway still needs:

- TLS/HTTPS;
- infrastructure rate limits and cost controls;
- strict server-side validation of any real model output against `schemas/behavior-plan.v0.1.schema.json`;
- bounded timeouts;
- explicit provider configuration;
- deployment-specific abuse controls;
- a privacy review of hosting-provider access logs;
- no requirement for cookies, sessions, VRChat user IDs, or raw conversation transcripts.

The reference response is deterministic content assembled from the route tuple. Replacing that content generator with an LLM is a separate implementation layer and must not weaken the route or BehaviorPlan validation boundaries.
