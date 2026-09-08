# 17 — Theme Art Bible v2

**v1.3 · 2026-08-28 · status: Medieval + Solar + Biopunk + Industrial shipped (37/72 units), four themes specced**

> Companion to [13-juice-v2.md](13-juice-v2.md). Doc 13 covers *how things feel when they move*; this doc covers *what they are made of*. Where they conflict on animation timing, 13 wins.

This is the controlling document for per-theme unit art. It records what is actually built today, specifies the nine units for each of the eight themes, and gives an implementation order with the shared code each step needs.

---

## 1. How unit art works today

Read this before specifying anything — the constraints below are why the specs are shaped the way they are.

### 1.1 The pipeline

Unit sprites are **rasterized procedurally at runtime**, not imported. There are no PNGs to author.

```
ThemeLocale.ArtId(canonicalId)   canonical id -> per-theme art id
        |
NeonArt.BuildUnit3D(artId, isPlayer, part, pose)
        |
   switch (artId) -> P3DBuild<Name>(px, R, p, L, H, fillL, part, pose)
        |
   writes into Color[128*128], y-UP, then P3DOutline() -> Texture2D
```

- Canvas is **128 x 128, y increases upward** (Unity texture convention). Every builder works in that space with `s = R/128 = 1`.
- Enemies are the **same texture UV-mirrored**. Never author a separate enemy build.
- Textures are cached per `(artId, isPlayer, part, pose)`. Adding poses multiplies texture count — keep the pose contract fixed.

### 1.2 The pose contract

Six frames, driven by `SideJoints[6,16]` (8 joint xy pairs per pose):

| Pose | Meaning | Notes |
|---|---|---|
| 0 | Idle | Resting stance |
| 1 | Walk A | Near leg forward |
| 2 | Walk B | Near leg back |
| 3 | Attack windup | Weapon hauled back |
| 4 | Attack strike | Weapon extended, projectile released |
| 5 | Flinch | Braced on rear foot, head snapped back |

Registries in `NeonArt.cs` control which ids get what:

- `HasPoses(id)` — has a walk cycle
- `HasAttackPoses(id)` — has windup/strike frames
- `UsesSideRig(id)` — uses the shared humanoid rig `P3DSideRig`
- `HasWeaponPart(id)` — weapon is a separate layer (`part` 0 full / 1 body / 2 weapon)
- `IsAuthoredArt(id)` — texture colours itself; the view layer must not multiply it by the team tint

**Every new builder must be added to the relevant registries.** A builder that is dispatched but not registered animates as a static idle.

### 1.3 Two drawing styles

**Lit primitives** (the original style): `P3DSphere`, `P3DCylinder`, `P3DLimb`, `P3DVisor`, `P3DChestOverlay`. Blinn-Phong shaded against a light rig `L = (-0.45, 0.70, 0.55)`. Reads as smooth plastic.

**Authored plates** (the Medieval style): `P3DPlate` (gradient polygon + dark outline), `P3DPolyGrad`, `P3DPolyLine`, `P3DBlade`, `RotP`. Reads as painted, hand-drawn equipment. This is the style all new work uses.

Mixed use is correct and expected: limbs stay `P3DLimb`, equipment is plates pinned to the rig's joint positions so it inherits animation for free.

### 1.4 The livery rule

Tinting every surface made the enemy a solid red silhouette. Team identity now works like real heraldry:

| Helper | Team influence | Use for |
|---|---|---|
| `ArtSteelBase/Hi/Dk/Far(p)` | 20% bias over gunmetal | Armour, machinery, hulls |
| `ArtLivery / ArtLiveryDk(p)` | Full team colour | Cape, plume, heraldry, sash, banner, glow |
| `ArtGold / ArtLeather / ArtCloth / ArtRune` | None — fixed | Trim, straps, accents |

Roughly **20–30% of a unit's pixels should be chromatic**. If the enemy version reads as a solid colour block, too much is on livery.

Any new authored builder must also be added to `NeonArt.IsAuthoredArt()`, or `BattlefieldView.StyleView` multiplies the whole sprite by the team tint and flattens the livery back out.

---

## 2. Coverage as of 2026-08-28

| # | Theme | Own art | Shared | Base set | State |
|---|---|---|---|---|---|
| 0 | Cyber Blue | 0 | 0 | 9 | Native set — but still lit primitives |
| 1 | Synthwave Purple | 0 | 3 | 6 | Renames only |
| 2 | **Biopunk Green** | **9** | 0 | 0 | **Complete** — grown set, own geometry |
| 3 | **Medieval Crimson** | **9** | 0 | 0 | **Complete** |
| 4 | **Industrial Ghost** | **9** | 0 | 0 | **Complete** — stamped set, own geometry |
| 5 | Sakura Dusk | 0 | 2 | 7 | Renames only |
| 6 | **Solar Forge** | **9** | 0 | 0 | **Complete** — own geometry, cast and fired |
| 7 | Dawn Light | 1 | 1 | 7 | `wanderer`, rebuilt on plates |

Authored-art total: **37 of 72**. Verified by `coverage.py`, which walks every
`ThemeLocale.ArtId` remap, checks it has a `BuildUnit3D` case, and reports its registry flags.

"Shared" means the id is remapped but resolves to a builder shared across themes. After this pass that is only `holobot` and `wisp` (both hit `P3DBuildHoloOrb`) and `racer` / `kunoichi` (both hit `P3DBuildTrooper`).

**The headline: three of eight themes are still the cyber roster wearing a different name** — Cyber, Synthwave and Sakura — plus Dawn at one authored unit.

---

## 3. Theme specifications

Nine canonical slots per theme: `drone · trooper · sniper · mech · shield-bot · interceptor · hacker · titan · turret`.

Each spec gives the art id, the silhouette idea, and the one prop that must read at 30px. **The silhouette is the deliverable** — at battlefield scale, players read outline before detail.

### 3.0 CYBER BLUE — "hard-edge military sci-fi"

Materials: brushed gunmetal, emissive cyan seams, visor slits, hard bevels.
Signature move: emissive edge-light on every plate boundary.

| Slot | Art id | Silhouette | Reads-at-30px prop |
|---|---|---|---|
| drone | `drone` | Quadrotor, wide stance | Four blurred rotor arcs |
| trooper | `trooper` | Plate-carrier infantry | Horizontal visor bar |
| sniper | `sniper` | Kneeling, very long barrel | Barrel length + scope glint |
| mech | `mech` | Reverse-joint biped | Shoulder cannon overhang |
| shield-bot | `shield-bot` | Squat, wide | Hex energy slab |
| interceptor | `interceptor` | Delta wing | Twin thruster glow |
| hacker | `hacker` | Hooded, no weapon | Wrist tendril emitters |
| titan | `titan` | Massive, layered skirt | Twin shoulder cannons |
| turret | `turret` | Tripod, no legs | Twin barrels + ammo drum |

**Status: highest priority.** These nine builders feed three themes (0, 1, 5) now that Solar and Biopunk have their own. Rebuilding them on authored plates is the single largest visual upgrade left, and it costs nothing in new silhouettes.

### 3.1 SYNTHWAVE PURPLE — "chrome and horizon"

Materials: chrome with a sunset-gradient reflection, neon grid decals, wireframe.
Signature move: flat plates filled with a magenta-to-cyan horizon gradient plus 3–4 grid lines.

| Slot | Art id | Silhouette | Reads-at-30px prop |
|---|---|---|---|
| drone | `holobot` | Orb inside rotating rings | Ring tilt |
| trooper | `racer` | Jacketed rider, visor helmet | Visor stripe |
| sniper | `lasergrid` | Standing, thin beam rifle | Beam line, not a barrel |
| mech | `cruiser` | Low chassis on hover pads | Ground gap + pad glow |
| shield-bot | `bouncer` | Chrome slab, neon edge | Edge-lit rectangle |
| interceptor | `speeder` | **Hover-bike with rider** | Rider silhouette above the bike |
| hacker | `synth` | Wireframe cloak, keytar caster | Held instrument |
| titan | `monolith` | Chrome obelisk torso | Gradient face slab |
| turret | `pylon` | Neon-ring emitter on tripod | Concentric rings |

**Cheapest path:** a decal layer over the base set gets 7 of 9. Only `speeder` needs a genuinely new silhouette — the delta wing reads as generic sci-fi in every theme it appears in.

### 3.2 BIOPUNK GREEN — "chitin and spore"

Materials: wet chitin, fungal growth, bioluminescent seams, exposed sacs.
Signature move: **asymmetry** — the left limb never matches the right.

| Slot | Art id | Silhouette | Reads-at-30px prop |
|---|---|---|---|
| drone | `spore` | Floating sac, trailing filaments | Filament trail |
| trooper | `mutant` | Asymmetric limbs, bone club | One oversized arm |
| sniper | `stinger` | Crouched, long proboscis | Needle proboscis |
| mech | `crawler` | Six-legged carapace | Leg count |
| shield-bot | `carapace` | Fungal shield growth | Bracket-fungus shield |
| interceptor | `swarm` | Winged insect | Twin membrane wings |
| hacker | `mycelium` | Robed, spore cloud | Cloud instead of a weapon |
| titan | `hive` | Bulbous hive body, glowing core | Exposed core sac |
| turret | `pod` | Rooted plant, seed spikes | Root spread at the base |

**Start at `titan`.** A hive-body apex unit sells the whole theme by itself and is the unit players stare at longest.

### 3.3 MEDIEVAL CRIMSON — SHIPPED

| Slot | Art id | Builder |
|---|---|---|
| drone | `pigeon` | `P3DBuildPigeon` |
| trooper | `knight` | `P3DBuildKnight` |
| sniper | `archer` | `P3DBuildArcher` |
| mech | `siege` | `P3DBuildSiege` |
| shield-bot | `paladin` | `P3DBuildPaladin` |
| interceptor | `rogue` | `P3DBuildRogue` |
| hacker | `wizard` | `P3DBuildWizard` |
| titan | `golem` | `P3DBuildGolem` |
| turret | `ballista` | `P3DBuildBallista` |

Remaining polish is tracked in §6.

### 3.4 INDUSTRIAL GHOST — "riveted iron and coal"

Materials: riveted iron, soot, hazard chevrons, exposed pistons, brass gauges.
Signature move: rivet rows along every plate edge; a visible firebox glow on powered units.

| Slot | Art id | Silhouette | Reads-at-30px prop |
|---|---|---|---|
| drone | `rivetbot` | Small boxy flyer, ducted fan | Single ducted fan ring |
| trooper | `worker` | **Built** — hard hat, overalls | Hard hat |
| sniper | `gunner` | Standing, welding mask | Mask + long rifle |
| mech | `crane` | Gantry arm on treads | Arm overhang + treads |
| shield-bot | `bulkhead` | Riveted plate door as shield | Rectangular riveted slab |
| interceptor | `ornithopter` | Flapping mechanical wings | Wing frame struts |
| hacker | `engineer` | Coil device, tool belt | Arcing coil |
| titan | `furnace` | Boiler-body walker | Glowing firebox in the chest |
| turret | `gatling` | Coal-fed rotary | Barrel cluster + hopper |

**Cheapest theme to complete.** Industrial is mechanically the same family as the base set, so it completes as a *reskin, not a re-silhouette*: rivets, chevrons, soot, pistons over existing geometry.

### 3.5 SAKURA DUSK — "paper, lacquer, blossom"

Materials: matte paper, lacquer red/black, gold leaf, woven cord. Almost no bare metal.
Signature move: paper translucency — flat fills with a visible frame behind them.

| Slot | Art id | Silhouette | Reads-at-30px prop |
|---|---|---|---|
| drone | `lantern` | Paper lantern, flame inside | Lantern ribs |
| trooper | `kunoichi` | Naginata infantry | Polearm length |
| sniper | `yumi` | Asymmetric longbow | Off-centre bow grip |
| mech | `palanquin` | Shrine carried on legs | Roof silhouette |
| shield-bot | `tetsu` | Lacquered tower guard | Broad lacquer shield |
| interceptor | `kite` | Paper-kite glider | Kite diamond + tail |
| hacker | `onmyoji` | Talisman caster | Floating paper talismans |
| titan | `oni` | Hulking, masked, club | Horned mask |
| turret | `torii` | Gate emplacement | Torii crossbeam |

**Biggest visual payoff, biggest gap.** Nothing in the roster looks like this yet. Cloth and paper suit the plate primitives better than plate armour does — **the wizard's robe rig ports here almost unchanged**.

### 3.6 SOLAR FORGE — "brass and molten"

Materials: brass and bronze, molten orange seams, sun-disc heraldry, blackened iron.
Signature move: cracks that glow like cooling metal.

| Slot | Art id | Silhouette | Reads-at-30px prop |
|---|---|---|---|
| drone | `ember` | Small flame-cored flyer | Trailing ember |
| trooper | `guardian` | Brass plate, sun crest | Sun crest on the chest |
| sniper | `raycaster` | Focusing-lens rifle | Lens disc |
| mech | `forgewalker` | Bellows-driven biped | Bellows on the back |
| shield-bot | `aegis` | Sun-disc shield | Circular disc (contrast to the kite/tower shields) |
| interceptor | `phoenix` | Winged flame flyer | Flame wing trail |
| hacker | `pyromancer` | Robed, brazier staff | Brazier flame |
| titan | `colossus` | Bronze, molten-veined | Glowing chest seam |
| turret | `heliostat` | Mirror array on a mount | Mirror panel |

**The cheapest full theme in the game.** Solar amber sits directly beside the gold already hardcoded in `ArtGold`. Once the palette work in §5.1 lands, the nine Medieval builders re-palettise into a sun-forged set — same geometry, brass and molten seams instead of steel and heraldry — **without authoring a single new shape**.

### 3.7 DAWN LIGHT — "cloth and light"

Materials: draped cloth, soft light, weathered wood, almost no metal.
Signature move: hems that fade to translucent; light sources inside the cloth.

| Slot | Art id | Silhouette | Reads-at-30px prop |
|---|---|---|---|
| drone | `sprite` | Mote of light with a ribbon | Ribbon trail |
| trooper | `wanderer` | **Built** — cloaked, legless drape | Drape silhouette |
| sniper | `seeker` | Staff-bow of light | Light-string bow |
| mech | `caravan` | Cloth-draped walker | Draped canopy |
| shield-bot | `ward` | Standing light barrier | Translucent barrier plane |
| interceptor | `glider` | Cloth wings | Wide cloth wingspan |
| hacker | `oracle` | Robed, floating sigils | Sigil ring |
| titan | `sentinel` | Robed colossus | Hood + scale |
| turret | `beacon` | Standing stone with light | Stone monolith |

Second-cheapest theme after Solar: most slots share the robe rig that `wizard` and `wanderer` already use.

---

## 4. Implementation order

Ordered by payoff per unit of work, not by theme number.

| Phase | Work | Why here |
|---|---|---|
| ~~**A**~~ | ~~Palette indirection (§5.1)~~ | **LANDED** — `_artSets[8]`, `Art.Metal` feeds `ArtSteelBase`, cache keys carry the theme |
| **B** | Cyber rebuilt on authored plates (9 builders) | Feeds themes 0,1,5 simultaneously — biggest remaining win |
| ~~**C**~~ | ~~Solar via palette swap of Medieval~~ | **LANDED** — 9 remaps + 9 dispatch aliases, 0 new geometry |
| ~~**D**~~ | ~~Industrial reskin~~ | **LANDED** — 8 new builders plus the hazard-chevron, piston and gauge primitives |
| **E** | Synthwave decal layer + `speeder` (1 new shape) | Cheap; only one genuine silhouette |
| **F** | Sakura full set (9 new) | Largest payoff, largest cost — do it when the primitives are mature |
| ~~**G**~~ | ~~Biopunk full set~~ | **LANDED** — 9 builders plus the two organic primitives |
| **H** | Dawn set on the robe rig (7 new) | Mostly rig reuse |

Medieval polish (§6) runs alongside, not in a phase.

---

## 5. Shared code this needs

### 5.1 Palette indirection — LANDED

`ArtGold`, `ArtGoldHi`, `ArtGoldDk`, `ArtLeather`, `ArtCloth`, `ArtClothDk`, `ArtRune` and
`ArtOutline` were `static readonly Color` constants. They are now get-only properties reading
from `_artSets[GameSettings.ThemeIndex]`, so **no call site changed** and every existing builder
became theme-aware at once. `ArtSteelBase` derives from `Art.Metal` rather than a hardcoded
gunmetal, which is what lets Solar read as brass.

The unit texture cache keys now carry `_t{ArtThemeIdx}`. Without that, shared ids
(`sniper`, `mech`, `turret`…) would serve one theme's palette to another.

The shipped shape:

```csharp
struct ArtSet {
    public Color Trim, TrimHi, TrimDk;   // gold      -> brass / chrome / lacquer
    public Color Strap, Cloth, ClothDk;  // leather    -> cord / rubber / paper
    public Color Rune;                   // rune glow  -> molten / neon / spore
    public Color Outline;
}
static ArtSet Art => _artSets[GameSettings.ThemeIndex];
```

Then every `ArtGold` reference becomes `Art.Trim`, and Solar Forge is a data row rather than nine new builders. **This is the highest-leverage change in the document.**

### 5.2 New primitives

| Primitive | For | Notes |
|---|---|---|
| `P3DCloth(a, b, sag, col)` | Sakura, Dawn | Draped quad with catenary sag; the missing piece for cloth themes |
| `P3DGridDecal(pts, spacing, col)` | Synthwave | Parallel lines clipped to a polygon |
| `P3DRivetRow(a, b, n, col)` | Industrial | Dots along an edge |
| `P3DOrganic(pts, wobble, seed)` | Biopunk | Polygon with per-vertex jitter for asymmetry |
| `P3DRotorArc(cx, cy, r, sweep)` | Cyber, Industrial | Blur smear that reads as rotation |

### 5.3 Checklist for every new builder

1. Write `P3DBuild<Name>` in `NeonArt.cs`, y-up, `s = R/128`
2. Add the case to the **`BuildUnit3D` switch only** — never the legacy 2D `BuildUnit`
3. Register in `HasPoses`, `HasAttackPoses`, `UsesSideRig` / `HasWeaponPart` as applicable
4. Add to `IsAuthoredArt` if it uses plates + livery
5. Add the remap to `ThemeLocale.ArtId`
6. Add flavour text to `TooltipSystem` (`*TroopBody` and `TroopAttackAnim`)
7. Verify the enemy version is 20–30% chromatic, not a solid block

---

## 6. Open items

Carried from the 2026-08-28 session. Ordered by impact.

| # | Item | Where | Notes |
|---|---|---|---|
| 1 | **Authored plates are lit from below** | `P3DPolyGrad` | `t = (y - minY)/span` with y-up puts the highlight colour at the *bottom* of every plate, while the light rig is from above. Every cuirass, helm, shield and stone slab is shaded upside-down relative to the spheres and limbs beside it. One-line fix (swap the lerp endpoints), but it changes all nine Medieval units at once — wants a screenshot check first. |
| 2 | Pigeon has no leg | `P3DBuildPigeon` | Scroll is tied to a line, not to a drawn leg |
| 3 | Rogue wing membrane is a flat fill | `P3DBuildRogue` | Needs a gradient so the wing reads as translucent |
| 4 | Siege payload has no throw arc | `P3DBuildSiege` | Nothing visibly leaves the sling between poses 3 and 4 |
| 5 | `drone` air-flag inconsistency | `TroopStats` vs `BattleSession` | `TroopStats` calls the drone a "fast aerial unit", but the runtime spec has `IsAir = false`. Only `interceptor` is actually air. Pick one and make the copy match. |
| 6 | Themes 0/1/2/5 still use sci-fi copy | `TooltipSystem` | Medieval, Industrial, Solar and Dawn now have their own bodies and attack text. The four without their own art keep the cyber copy, which is still accurate for them. |
| 7 | Dead helpers after the rebuild | `NeonArt` | `P3DCloak`, `P3DStaff`, `P3DWandererEyes` are now referenced only by their own definitions. Harmless (unused private methods raise no warning) but worth deleting. |
| 8 | Shared orb drone has no walk cycle | `P3DBuildHoloOrb` | `holobot` / `spore` / `wisp` are absent from `HasPoses`, so Synthwave, and Sakura drones are static in the air (spore now has its own builder). Fold into phase B. |

---

## 7. Session log — 2026-08-28

### Pass 4 — Industrial, and the level-select rebuild

- **Industrial complete, 9/9.** rivetbot, worker, gunner, crane, bulkhead, ornithopter,
  engineer, furnace, gatling. Premise: Medieval is hand-forged for one owner, Solar is cast and
  fired, Biopunk is grown — Industrial is **stamped out by the thousand and repaired badly**.
  Solar was the theme it had to fight hardest to stay clear of, since both are metal; the
  separation is cast versus stamped.
- **Three new primitives.** `IndHazard` fills a polygon with 45-degree chevrons by testing
  `(x+y) mod period` per pixel — `P3DPolyGrad` picks a colour per scanline, so it cannot stripe
  a shape, and the chevron is the theme's signature mark. `IndPiston` draws a dark sleeve then a
  bright exposed rod that extends with the pose. `IndGauge` is a brass dial with a needle.
- **Level select rebuilt to Option D.** The grid was spending 600 canvas units to show twenty
  two-digit numbers while the detail panel was squeezed into what was left, with 382 units of
  dead space under the footer. The grid is now 5x4 at 140x76 (388u, a 212u saving), the panel
  claims all of it, and troop chips go 86x74 to 200x168 in rows of three — wide enough that
  FORGEWALKER and ORNITHOPTER fit on **one line**, so no name wraps or shrinks any more.
- **Scroller, rebuilt safely.** The first attempt reparented every child into a **zero-sized**
  rect, so stretch-anchored children (troop modal, settings overlay, demo arena) collapsed to a
  point at screen centre. Three rules now prevent that: the content rect is full-size and moved
  by `anchoredPosition`; full-screen overlays are excluded **structurally** (any child already
  stretched to the canvas is an overlay, so a modal added later is skipped automatically); and
  the viewport is full-screen so no child shifts on reparent.
- **Accurate hint copy.** The old line described the input and never said what you were trying
  to do. Read from the simulation: a match fires a `ResourcePayout`, resources buy deploys, each
  ground lane holds two pylons at x=33 and x=66, holding both grants lane control, and the battle
  ends when a core reaches zero. Now two lines — "Break the enemy core to win" over
  "Match gems for resources · Deploy to hold pylons".
- **Deploy cards.** Names best-fit across two lines instead of clipping at five characters, and
  the cooldown is a radial ring around the icon; the top-down veil is a flat dim and the bottom
  bar is retired.
- **Display names aligned** for Industrial — CRAWLER had been given to the Industrial mech while
  it is the Biopunk mech, and COLOSSUS clashed with the Solar titan.

### Pass 3 — Biopunk, and the sizing fixes

- **Biopunk complete, 9/9.** spore, mutant, stinger, crawler, carapace, swarm, mycelium, hive,
  pod — all own geometry. Premise: Medieval is hand-forged, Solar is cast and fired, Biopunk is
  **grown** — chitin, bone, sinew, fruiting bodies, translucent sacs lit from inside — and
  deliberately **asymmetric** where the other two finished themes are symmetric.
- **Two new primitives.** `P3DSac` draws a translucent membrane with an interior gradient, veins,
  a nucleus and a wet specular. Nothing else in the set draws interior detail, so organ-bodied
  units read as solid blobs without it. `P3DChitin` adds deterministic per-vertex jitter so no
  two shells match; the seed is fixed per plate so it does not shimmer between pose frames.
- **Flyer strobing fixed.** Air units swapped walk frames at 26Hz — correct for a rotor smear,
  but the authored flyers move a whole wing between frames, so it read as the sprite glitching.
  Authored art now flaps at 5.5Hz, phase-staggered per unit.
- **Deploy card names.** `FontH2` renders at 31pt in portrait; a card is 106 units wide, which
  fits five characters. Every theme had at least one name that clipped. Now best-fit across two
  lines — generic, so it holds for any future theme rather than being patched per roster.
- **Radial cooldown ring.** New `NeonArt.CooldownRing()` sprite — `Image.Type.Filled` needs a
  real Sprite, a RawImage cannot do radial fill. The top-down veil is now a flat dim and the
  bottom bar is retired; two meters for one value was noise.
- **Level-select scrolling.** Centre-anchored children reparented into one mover with drag and
  wheel handling; the top bar stays pinned. Range is measured from the children that exist.
- **Display names aligned to the art** for Solar and Biopunk. Biopunk had STINGER on the
  interceptor while the sniper is the unit with the proboscis.

### Pass 2 — palette system, Solar, and two rebuilds

- **Phase A landed.** `ArtSet` struct + `_artSets[8]`; the eight fixed-colour names became
  theme-indexed properties, so no call site changed. `ArtSteelBase` now lerps from `Art.Metal`.
  Texture cache keys gained `_t{ArtThemeIdx}`.
- **Phase C landed — Solar Forge is complete.** Nine `ThemeLocale` remaps
  (`ember`, `guardian`, `raycaster`, `forgewalker`, `aegis`, `phoenix`, `pyromancer`,
  `colossus`, `heliostat`) aliased onto the nine Medieval builders, registered across
  `HasPoses` / `HasAttackPoses` / `UsesSideRig` / `HasWeaponPart` / `IsAuthoredArt`.
  `guardian` moved off the shared trooper rig onto `P3DBuildKnight`.
- **`worker` rebuilt as authored art.** It was the stock trooper rig with a yellow sphere on
  its head. Now: canvas overalls over a riveted iron breastplate, bib straps, tool belt with
  hanging tools, livery armband, shoulder pads that rotate with their own upper arm, a
  hazard-striped hard hat with a brim, and a rivet gun that kicks on the strike.
- **`wanderer` rebuilt as authored art.** It was a front-facing cloak blob of cylinders with
  no legs, so it never read as walking. Now side-profile on the joint table: boots below the
  hem, travelling pack and bedroll, open travelling cloak with fold shading and a gold hem
  band, deep hood distinct from the wizard cowl, trailing scarf, and a staff with a hung
  lantern that flares on the cast.
- **Copy.** Added `SolarTroopBody` and `DawnTroopBody`, a Solar branch in `TroopAttackAnim`,
  and Industrial/Dawn trooper attack lines for the two rebuilds.
- **New check.** `coverage.py` walks every theme's nine slots, resolves the art id through
  `ThemeLocale`, and verifies a `BuildUnit3D` case exists plus its registry flags — catching
  the "renders as a grey default sphere" class of bug that no compiler check would find.

### Pass 1 — compile fix and readability

Changes landed alongside this document:

- **Compile fix.** Archer/paladin/ballista dispatch lines had been injected into *both* the `BuildUnit3D` switch and the legacy 2D `BuildUnit` switch (identical `case` labels, unbounded string replace). 20 x CS0103. Removed from the legacy switch. A scope scanner now walks every `P3DBuild*` call site and checks each argument against its enclosing method signature.
- **Livery reaching the screen.** `BattlefieldView.StyleView` was multiplying the whole sprite by the theme team tint, which crushed steel, gold, leather and stone to red on enemy units and undid the livery system entirely. Authored-art ids now take a 22% team bias; the glow halo keeps the full team colour.
- **Golem `sway`** was computed and never used (CS0219). Now drives a counter-rock on the skull.
- **Level-select portrait reflow.** Four separate collisions: GEM TYPES header sat on the gem icons, TROOPS sat on the gem labels, and the second troop-chip row ran through DIFFICULTY and REWARD. Portrait now has its own layout cursor, verified clear with >= 4px between every glyph span.
- **Troop modal border.** The accent border was a *child* of the panel — in uGUI a parent's Image draws before its children, so it covered the entire panel and the modal rendered as one flat sheet of theme accent (a solid red card in Medieval). Moved to a sibling drawn underneath.
- **Modal typography** raised throughout (name 22 -> 30, stats 24 -> 29, labels 13 -> 16, body 18 -> 21) with brighter text colours.
- **Modal copy** now uses `TooltipSystem`, which already had per-theme text, instead of the sci-fi strings in `TroopStats`. Added `TroopAttackAnim` with a Medieval set.
- **Demo air targeting.** The demo let a Knight trade blows with a Rogue. In the runtime specs only `interceptor` and `turret` have `TargetsAir`, so a ground unit can never damage a flier. Demo damage is now gated by the same rule, and the matchup header says `CANNOT REACH AIR` when it applies.
