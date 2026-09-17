# Map v2 — proposed motion and interaction

Clean composition: `02-layout-river.png`. Generated using built-in imagegen; source and exact edit prompt are retained. Flattened static concept only, no implemented effects or new 3D asset. Six level numbers, completion checks, locks, and continuous sequential road were visually inspected. River runs from upper left to bottom center-right. The sole bridge now carries the route between levels 1 and 2. Center uses lighter green grass.

## Proposed effect locations

| Element | Location | Motion | Trigger |
|---|---|---|---|
| River surface | Entire visible watercourse | Slow directional highlights and sparse foam, constrained to water mask | While map visible |
| Waterfalls | Upper-left source and lower outlet | Looping water strip, soft spray at impact | While map visible |
| Hero | Current available platform | Idle; authored walk to next node including bridge crossing | Idle / successful level completion |
| Active platform | Under current hero | Slow warm rim brightness variation, stable platform geometry | While available |
| Platform completion/unlock | Completed and next nodes | Check appears; lock fades and new rim lights once | After saved victory |
| Plants | Rightmost canopy, lower-left foreground branches, small bank grass beside bridge | Very slight independent sway, static trunks/rocks | While map visible |
| Atmospheric motes | Two small clearings off the road: right of level 3, beneath upper-right tree | Sparse slow translucent motes, no trails across labels | Optional polish |
| Primary button and available node | Bottom-right and current platform | Gentle hover highlight, small press response | Pointer / touch |
| Hero reaction | Current platform | Optional short head turn or wave | Later polish; avoid interfering with level launch |

Bridge stays rigid. Walking uses the bridge centerline; no wobble or bounce. No decorative click rewards, water interactions, or new game mechanics.

## Asset preparation

Keep terrain static. Produce water masks, flowing highlights/foam, waterfall strips, spray, selected foliage cutouts with clean background behind them, separate node states, hero, and contact shadow. Do not animate baked trees over identical baked trees. Split the bridge into appropriate back/front occlusion layers if rails overlap the walking hero. Keep a static river fallback; effects are disabled when the screen is hidden. Low-motion mode keeps water subtle and removes foliage/mote motion.

Priority: water + hero idle + current-node feedback; then victory transition and walking; then edge foliage; atmospheric motes last. Route, water, and all overlays must share the same map transform. Portrait composition needs separate placement verification.
