# Glimblehop — Art Bible v1

Concept references generated with the built-in imagegen tool. These are flattened raster illustrations, not production UI, game assets, exact geometry or implemented mechanics. English is the target interface language.

## Sheets

- [Main menu — landscape](00-menu-landscape.png)
- [Main menu — portrait](00-menu-portrait.png)
- [01 Character design](01-characters.png): proportions, turnarounds, expressions, team differentiation and accessories.
- [02 World and tile kit](02-world.png): proposed woodland, mushroom and snowy environments; modular tiles, props and wayfinding.
- [03 Play screen and movement](03-gameplay.png): proposed board composition, movement token selection, route preview and confirmation.
- [04 Journey](04-journey.png): campaign map, rest, potion and reward presentation proposals.
- [05 UI kit and motion](05-ui-motion.png): buttons, icons, modal windows, wardrobe, feedback and animation poses.

## Approved visual direction

Warm handcrafted wooden and clay tabletop dioramas; playful green goblins with large heads and ears; cream tiles, moss greens, soft shadows and restrained decoration. Keep the route legible and characters grounded. Both teams should share proportions and use an emblem as well as color for identification.

No dice or pip imagery. Movement is expressed with different step lengths: footprint, running boot and winged boot. Wayfinding uses forest trees, campfire/tent and potion bottle symbols. Preserve the readable English GLIMBLEHOP wordmark and main-menu hierarchy.

## Design proposals, not approved rules

The 1/3/5 lengths are illustrative. How tokens are obtained, how many actions a turn allows, exact movement restrictions and whether a hop can bypass anything remain undecided. Three goblins per team and a 0/2 finish counter are prototype proposals. Biomes, cosmetic slots, potion effects and rewards are also proposals; the illustrated +20 is not a balance value.

Do not derive tile counts, connectivity or movement rules from generated drawings. In sheet 03 the highlighted-tile counts are illustrative and need correction in an actual UI prototype. Decorative signs on the campaign sheet are not a canonical node-type mapping: use forest/race, rest, potion and reward definitions consistently in implementation. Small generated labels and repeated symbols require a typography/icon consistency pass before production.

## Production handoff

Use these sheets as art direction, then author separate meshes, textures, animation clips, vector icons and responsive UI. Do not slice these images and assume production-ready assets. Match horizontal and vertical layouts independently. Reduce environmental detail from the concepts wherever it competes with valid destinations or characters.

Next prototype: one woodland board, one reusable goblin with team variations, three movement-token icons, selection/destination feedback and a responsive menu. Validate readability and movement rules before expanding the world or economy.

Exact generation prompts for the five new sheets are in [prompts.json](prompts.json). The menu sheets were approved earlier and copied here unchanged. No gameplay code was changed by this concept-art task.
