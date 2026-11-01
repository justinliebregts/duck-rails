# Duck Rails Art Guide

Version 0.3 | October 30, 2026 | Creative direction draft

## 01 The visual identity

**A battered little railway carries expressive cartoon ducks through an abandoned modern world.**

The train feels familiar and worth returning to. Beyond its lights, ordinary places become uncertain. Fear comes from distance, concealment and pursuit; comedy comes from the crew's faces, movement and decisions. Every scene should make a rescue, a hurried boarding or a ridiculous argument easy to see.

### Established design

Modern setting; up to eight cartoon ducks on an expedition; expressive eyes; zombie-duck enemies; a moving coal-fueled train; shared physical equipment; nighttime scavenging and an escape into dawn. The train is mostly open air; a small front enclosure is optional. Design windows for the user-reported typical eye line of approximately 0.5 metres above the local floor. Preserve Duckz locomotion, grabbing, backpack use and character identity. Enemies remain within their authored blocks and off the train in V1.

### Existing asset style and proposed treatment

Confirmed October 30: match DuckZ's low-poly geometry and visibly pixelated textures, using the supplied avatar and Quackville screenshots as the style baseline. Preserve its faceted forms and expressive characters. Put coarse texture pixels on simple meshes; do not build every texel as a voxel. The palette, industrial styling and effects remain proposals. Performance must be measured on Quest 3; a generated render does not prove an asset budget.

### Three rules for every asset

- **Read the action first.** Faces, hands, threats, supplies and escape routes take priority over decorative detail. A small gameplay object may need more contrast than a large building.
- **Make the world believable at duck scale.** Broad steps, thick handles and legible controls should feel deliberately built for the existing interaction reach. Keep familiar real-world functions even when proportions are simplified.
- **Spend detail where players gather.** Give the furnace, radio and boarding points character. Use quieter surfaces behind moving players and equipment.

### Shape and material language

Use simple box forms, low-sided cylinders and deliberate facets. Spend polygons on silhouettes and usable handles; express seams, wear and small hardware in coarse pixel textures. Match the reference assets' visible texel scale and preserve it in close views. Keep steel, rubber, concrete, timber and cloth distinct through color and pixel patterns. Avoid dense bevels, modeled microdetail, painterly surfaces and photoreal texture noise. Preserve existing character proportions and rigs.

**Direction boundaries:** modern utility railway rather than a Western theme; restrained industrial construction rather than ornate steampunk; stylized environments rather than photoreal rubble. Keep overt visual jokes sparse so players supply the comedy.

---

## 02 Color and lighting

**The train is the warm landmark. The scavenging world falls into cooler, quieter layers.**

These sRGB swatches are starting references for concept art and material families, not literal light intensities, exposure settings or fixed shader values. Tune their rendered appearance on Quest 3. Exact colors are proposed.

### Working palette

- **Night ink | #15232D:** distant silhouettes, sky family and deep recesses. Do not bury usable paths in this value.
- **Weathered steel | #485B63:** machinery and secondary structures; keep broad areas calm.
- **Railway teal | #287477:** proposed train paint and recurring railway identity. It is not a universal interaction cue.
- **Warm ivory | #E8DDC0:** sign faces, light neutral trim and readable lettering.
- **Safety amber | #E9AE45:** handles, boarding edges and caution markings; reserve it for useful emphasis.
- **Alarm red | #C74C3C:** immediate hazards and urgent states, paired with an icon, motion or plain language.
- **Dawn peach | #EFB58D:** the ending's sky and warm light family; let existing material colors remain recognizable.

### Compose four lighting situations

- **Compound:** welcoming pools of light with readable faces. Make boarding and private joining distinct destinations without filling the room with bright signs.
- **Train at night:** warm practical fixtures and furnace light establish a visible home. Keep controls, cargo and boarding edges readable when fuel is empty; the visual design must not imply the radio stops working.
- **Scavenging stop:** cool ambient separation, local practical lights and flashlight reveals. Preserve readable nearby footing while letting distant interiors and corners remain uncertain. Use occlusion and contrast to create fear.
- **Dawn ride:** hide the change inside the tunnel, then open into a softer, brighter horizon. Reduce oppressive contrast and reveal more distance. Avoid a white exposure flash.

### Light moving subjects deliberately

Faces and enemy silhouettes must separate from their background. An approaching threat needs a readable attack cue; total darkness is not an acceptable substitute. Coal should catch enough light to identify its shape without becoming a magical glowing collectible. Flares can dominate briefly, but must not erase the rescuer, downed duck or exit.

Review representative views with flashlight off and on, night vision active, and a flare burning. Check grayscale readability and recorded footage as well as the headset. Establish the lighting and probe approach for assembled blocks and the moving train before mass production; baked lighting is a workflow to prove, not a completed assumption.

---

## 03 Train and equipment

**Make the train recognizable from its silhouette and welcoming from every boarding approach.**

### Train design brief

Keep the crew area mostly open air. A small, lightly enclosed front is an option; avoid extending it into a closed passenger cabin. Explore compact industrial machinery, an integrated improvised coal furnace and a teal-and-ivory identity. Warm fixtures and amber touch points should guide the eye. These styling choices remain proposals within the confirmed open-air direction.

### Design from the low eye line

Use approximately 0.5 metres above the carriage deck as the typical eye-height reference supplied by the user for gorilla movement. Any front windows must extend low enough to see the track and nearby hazards from this height. Keep sills below that sightline; exact dimensions require in-rig review. Check the nose, dashboard, window frames, rails and stored bags for occlusion. Preserve views while moving low, without requiring players to stand up or climb to see out.

Protect room for eight ducks, full-size bags and loose equipment. Keep hands, faces and teammates visible across the carriage. Avoid decorative pipes crossing entrances, snagging grab space or hiding controls. Use engineering's existing reach, floor, boarding and interaction envelopes as the modeling reference.

### Give each station a clear job

- **Drive:** a substantial grip handle with legible OFF and GO positions. The art must explain push-to-go while gripping, pull-back-to-stop and release-to-stop. Avoid an appearance that suggests a latched throttle.
- **Furnace:** an obvious open mouth, a distinct rim and a gauge readable beside it. Show accepted coal with a brief localized ember response; empty fuel still leaves the crew able to navigate the carriage.
- **Horn and radio:** distinct pressable caps, clear HORN and TALK labels and a visible held state. Keep decoration clear of the physical contact surfaces.
- **Storage:** recognizable hooks and resting surfaces. Full-size backpacks remain accessible. Loose items still travel with the carriage; holders are convenience, not a visual explanation for unsupported cargo behavior.

### Equipment treatment

Replace cube coal with a chunky, irregular mesh that reads in the hand, on the ground and among bag contents. Give it a dark matte body with broad facets that catch light. Avoid ember glow on unburned coal.

Reuse the existing bat, flashlight, walkie-talkie, helmet, night-vision goggles and bags. First unify value range, roughness and wear where needed. Preserve recognizable silhouettes, grips, slots, rigging and functional geometry. Add new detail only where close inspection rewards it.

### Material placement

Painted steel belongs on the train body, darker rubber on grips, dull exposed metal on rubbed edges and soot around the furnace. Keep soot localized. Put scratches where hands, bags and cargo actually contact a surface. Do not add noisy dirt to gauges or instructional labels.

**First train review:** show the open-air silhouette, boarding access and a deck view with crew and supplies. Include a front section drawing marking the deck, 0.5-metre eye line, window sill and view toward the track. Final concepts and production models are subsequent deliverables.

---

## 04 Environments and the ending

**Finish Maintenance Depot first, then use its kit and lighting rules across the route.**

### The first finished stop

Give the depot a recognizable roofline and a large maintenance-bay opening. Frame the arrival so players can identify the building, the way in and the way back to the train. Place a strong landmark above the clutter line. Let a pool of work light suggest a useful area, with darker recesses creating uncertainty beyond it.

Use abandoned work rather than indiscriminate destruction: a repair stand, displaced crates, tools left out and an interrupted loading job. Keep floor clutter away from hands, coal and return routes. Dressing may create visual tension but must not silently change collision, enemy routes or encounter spacing.

### Build a shared kit

Start with wall and roof modules, door frames, concrete foundations, fences, gates, crates, pallets, work lamps, utility poles, railway signs and a small vegetation family. Reuse materials and proportions. Each location earns a few distinguishing shapes instead of a completely separate asset language.

### Landmark vocabulary for the twelve stops

- **Maintenance Depot:** sawtooth or stepped roof and an open repair bay.
- **Convenience Store / Roadside Motel:** a broad shop fascia / a tall roadside sign and repeating doors.
- **Lumber Yard / Container Yard:** stacked timber with a cutting shed / stacked container masses and an overhead gantry silhouette.
- **Passenger Station / Farm Silos:** platform canopy and station clock / clustered vertical silos.
- **Tunnel Works / Scrapyard:** heavy tunnel portal and work lights / an uneven skyline of compressed scrap and machinery.
- **Lakeside Campground / Electrical Substation:** shoreline opening with simple cabins / ordered transformer and insulator shapes.
- **Quarry:** exposed stepped rock faces and a loading structure.

These are proposed visual identifiers. Place decorative landmarks within the existing playable layouts; they do not authorize new traversal or enemy behaviors.

### Compound, bridge and dawn

Use the same railway identity in the compound. Public departure needs an unmistakable platform boundary, readable occupancy and countdown. The private terminal needs separate Create and Join actions and a readable five-digit code. Keep the shopping area visually secondary to meeting and boarding.

The bridge silhouette must visibly obstruct the route while raised. Advance warning signs, the external control, lowering motion and fully open passage must agree. Preserve manual braking and the established crash outcome. Design the tunnel to conceal the lighting transition, then reveal a broad dawn view that leaves room to watch the crew. The train remains the setting for shared relief and results.

---

## 05 Ducks and gameplay feedback

**Preserve the existing ducks. Improve the clarity of what they are feeling and doing.**

### Characters and threats

Match the supplied DuckZ avatars: large white eyes, small dark pupils, angular bills, compact legless torsos and long arms with hands. Retain their low-poly proportions, pixel textures, rigs and attachment points. Replace the initial sketches' ordinary bird placeholders. Check faces under train light, flashlight, flare and dawn. Cosmetics must preserve eyes, held-object visibility and separation from enemy silhouettes.

Develop zombie readability through the existing rig and animation vocabulary: a recognizable posture, visible attack anticipation, clear contact response and an obvious retreat. Separate enemies from friends using shape and movement as well as color. Start with one existing enemy type before expanding variations. Graphic gore is not the proposed visual focus; any inherited gore assets need a separate tone and audience decision.

### Proposed feedback treatments

- **Downed:** retain the existing local red overlay and immediate flare. Make the duck's lowered posture readable to teammates. Do not imply death, bleed-out or a countdown to elimination.
- **Reviving:** use restrained progress feedback close to the rescue interaction, synchronized with the existing rising-pitch sound. Keep the hands and duck visible. Completion should have a brief, unmistakable response; interruption must stop progress feedback.
- **Enemy repelled:** show a directional recoil followed by retreat. Avoid death-like collapse or effects that promise a kill.
- **Coal accepted:** a short furnace response and gauge change communicate consumption. World feedback must follow accepted gameplay state, not an optimistic local guess.
- **Bridge lowering:** motion and a legible progress state communicate the wait. Completion needs a distinct visible state beyond a color change.

### Signs and interface

Use a sturdy sans-serif face with open letterforms, simple pictograms and generous spacing. Keep practical labels short: GO, OFF, TALK, HORN. Validate type at its actual viewing distance. Use shape, words and motion together; red and green alone cannot explain state.

Avoid floating text across every object. Put instructions beside the relevant action, then let geometry and feedback do the work. Onboarding should clearly teach boarding, fuel, rescue and escape.

Use the same results-board layout for success and failure. Show SUCCESS or FAIL prominently, with each player's Escaped or Didn't make it status distinct from humorous awards. Keep the board readable for eight players and position it so the crew can still see one another and enjoy the ride.

**Effects rule:** small, localized bursts with readable timing. Smoke, sparks and flare particles must not conceal the next useful action or create persistent visual clutter.

---

## 06 Production and art review

**Prove one coherent art slice before dressing all twelve locations.**

### Production order

1. Use this guide to explore train concepts and a depot lighting sample.
2. Finish the train, depot and essential interaction props together, so their scale and materials agree.
3. Review them in Quest 3 and recorded gameplay, then refine the reusable kit.
4. Extend the kit across the remaining stops, compound, bridge, dawn ride and results presentation.
5. Produce a small themed cosmetic selection and final promotional images once the in-game identity holds together.

### Protect the playable prototype

The Week 3 builder can overwrite block and scene art. Before dressing content, establish protected art prefabs referenced by the builder or another arrangement that survives regeneration. This workflow remains to be implemented.

Keep visual meshes separate from gameplay collision and interaction components where practical. Preserve unit scale, track anchors, spawn markers, encounter spacing and the block connection contract. Any intentional geometry or collision change needs navigation review and an editor crawlgraph rebake where affected. Never depend on runtime baking.

Reuse existing Duck Rails character and equipment copies. Leave the source Duckz project untouched; inspect inherited assets before deciding what needs remodeling.

### Quest 3 art review gate

Review the train and depot on Quest 3. Set art budgets from profiling. Also review the full assembled route: five selected stops plus the finale remain loaded together.

- From arrival views and a 0.5-metre eye line above the deck, identify the route and hazards; check window sills, front bodywork, rails and cargo for occlusion.
- In darkness and under effects, distinguish friends, enemies, coal and usable controls.
- With eight players, check faces, boarding room, bag access and results text.
- Verify added art does not obstruct locomotion, grabs, loose cargo or boarding.
- Check night, night vision, flare and dawn for comfort, glare and clarity.
- Inspect moving-train lighting, block joins, shadow transitions and recorded footage.
- Record target-device frame time and memory before accepting the art pass. A beautiful desktop image is not headset acceptance.

### Status and references

DuckZ's low-poly and pixel-texture style, the open-air train and low eye line are confirmed. A generated train render explores that style; see the concept index. Exact dimensions, final meshes, depot art, lighting and headset review remain future work. Palette, detailed silhouette, feedback and inherited gore treatment remain proposals.

References: [V1 game design](game-design.md), [execution plan](execution-plan.md), [concept index](art-concepts.md), [Week 3 authoring](week3-session.md) and [Week 2 interactions](week2-session.md). Source assets: `Assets/Skins`, `Assets/Models`, `Assets/Resources/Rails` and `Assets/DuckRails/Blocks`.

The editable source is `docs/art-guide.md`; the designed reading copy is `output/pdf/duck-rails-art-guide.pdf`. Regenerate the PDF with `Tools/build-art-guide.py` using a Python environment with ReportLab installed.
