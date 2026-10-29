# Duck Rails project guidance

Read `docs/game-design.md` and `docs/execution-plan.md` before planning or implementing features.

## Project context
- Cooperative VR horror-comedy: up to eight cartoon ducks scavenge coal during a modern nighttime train journey and escape into dawn.
- Reuse Duckz rather than rebuilding its working interactions. The source location, engine version, and component paths have not been verified yet.
- Retain the existing Photon Realtime networking stack. Fusion migration is out of V1 scope. Verify whether the source uses direct Realtime APIs, PUN, or a mixture before implementation.
- One Photon region. Public compound departures and separate private code-based expeditions. Late joining aboard the train is required.
- Reliable networked loose-item motion is a launch requirement. Physical holders do not substitute for it.
- Authored blocks with prebaked crawlgraphs; no runtime navigation baking. Enemies stay within their block and off the train in V1.
- Existing Duckz capabilities described in the design are user-reported, not findings from a code audit.

## Working approach
- Implement and prove session architecture first, using actual moving-platform and shared-item state; then build the expedition loop.
- Preserve existing locomotion, grabbing, backpack, voice, and moving-platform feel.
- Reuse cosmetic purchasing infrastructure; do not introduce a new currency or commerce backend.
- Keep confirmed decisions separate from proposed defaults and unresolved questions. Do not silently promote a proposal into a requirement.
- Update the design and milestone checklist when decisions change or deliverables are verified.
- Test networked behavior across real clients and target headsets; a single editor instance is insufficient evidence.
- Do not mark features complete without the milestone acceptance evidence described in the execution plan.
- Do not migrate or modify the source Duckz project merely to import reusable systems without confirming the intended project boundary.
