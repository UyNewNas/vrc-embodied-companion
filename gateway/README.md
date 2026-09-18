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

## Run the live reference endpoint

```bash
cd gateway
python reference_server.py --host 127.0.0.1 --port 8787
```

Example:

```text
GET http://127.0.0.1:8787/v1/plan/default/quiet_company/warm/quiet/zh/2
```

A successful response is compatible with the current `BehaviorPlan v0.1` shape.

## Generate the trusted static fallback pack

The selected transport has 288 bounded routes per persona in v0.1. Do not author those URLs/responses by hand.

```bash
cd gateway
python generate_static_pack.py --output ../dist/static-pack --persona default
```

The generator writes:

```text
dist/static-pack/
├── route-manifest.json
└── v1/plan/default/<intent>/<relation>/<comfort>/<language>/<slot>.json
```

`route-manifest.json` is the authoritative transport/editor handoff. Each static response uses the same BehaviorPlan shape as the live reference endpoint.

## Generate the Unity editor route table

Unity should not need to understand the manifest's nested index metadata or reconstruct 288 paths itself. Flatten the reviewed manifest into a strict parallel-array artifact:

```bash
cd gateway
python generate_unity_route_table.py \
  --manifest ../dist/static-pack/route-manifest.json \
  --output ../dist/static-pack/unity-route-table.json
```

`unity-route-table.json` is intentionally host-free and JsonUtility-friendly. It contains exactly 288 entries in three parallel arrays (`route_keys`, `live_relative_paths`, and `static_relative_paths`) where **array index equals `route_index`**. The future Unity importer is expected to combine those reviewed relative paths with separately configured editor-time HTTPS base URLs before constructing serialized `VRCUrl[]` values.

The generator fails closed if the manifest version, route count, index order, stride contract, route tuple, or live/static path no longer matches transport v0.1. Its output contract is also frozen in `schemas/unity-route-table.v0.1.schema.json`.

### Editor route-index contract

Transport v0.1 assigns every route a dense stable `route_index` in `[0, 288)`. The manifest records the exact mixed-radix layout so a future Unity editor importer can serialize live and static `VRCUrl[]` tables in the same deterministic order rather than relying on hand-authored lookup tables.

The fixed v0.1 formula is:

```text
route_index = intent*48 + relation*16 + comfort*8 + language*4 + turn_slot
```

The code orders are frozen as part of `route_index_version = 0.1`:

```text
intent:   goodbye, greet, offer_hug, quiet_company, talk_light, walk_with_me
relation: familiar, new, warm
comfort:  neutral, quiet
language: en, zh
slot:     0, 1, 2, 3
```

For each route the manifest emits both `live_relative_path` and `static_relative_path`. They are deliberately different: the live reference endpoint uses `.../<slot>`, while the trusted static artifact uses `.../<slot>.json`. Editor tooling must not treat them as interchangeable.

If any code order or stride changes in a later transport version, `route_index_version` must change as well. Runtime code must reject a manifest/index version it does not understand rather than silently selecting the wrong URL.

Static output is intentionally reproducible: unlike the live endpoint's fresh UUID, each static file receives a deterministic `static:<digest>` artifact ID. That ID is **not** a network-attempt nonce. The world must still reject stale callbacks using its own in-flight route/turn state plus `IVRCStringDownload.GetUrl()`, as required by the RFC.

The generators refuse invalid persona IDs and never create query strings, free-form player data, user identifiers, or transcript-bearing paths.

## Tests

```bash
cd gateway
python -m unittest -v test_reference_server.py test_generate_static_pack.py
```

The live reference prototype contributes **9** HTTP/contract tests. The static-pack/index layer contributes **11** tests, and the Unity route-table handoff adds **5** more covering dense array alignment, reordering rejection, path-widening rejection, reproducible output, and the strict schema artifact. The combined suite is therefore **25 tests**.

These tests are **not** evidence that UdonSharp or `VRCStringDownloader` integration works; that proof remains #18 and is blocked on the runnable #2 world.

## Production boundary

Do not deploy `reference_server.py` as-is as the public service. A production live gateway still needs:

- TLS/HTTPS;
- infrastructure rate limits and cost controls;
- strict server-side validation of any real model output against `schemas/behavior-plan.v0.1.schema.json`;
- bounded timeouts;
- explicit provider configuration;
- deployment-specific abuse controls;
- a privacy review of hosting-provider access logs;
- no requirement for cookies, sessions, VRChat user IDs, or raw conversation transcripts.

The reference response is deterministic content assembled from the route tuple. Replacing that content generator with an LLM is a separate implementation layer and must not weaken the route or BehaviorPlan validation boundaries.
