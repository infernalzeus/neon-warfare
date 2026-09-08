# NEON WARFARE — Screen Flow, Wireframes & UI Design System
**Document 03 of 09 · v1.0 · 2026-06-12**

The UI fantasy: the player is operating **ARC//OS** — a netrunner's combat operating system. Every screen is a "program" inside that OS: boots, glitches, scanlines, holographic depth. Menus aren't pages; they're *windows into the Grid*.

---

# 1. SCREEN FLOW MAP

```
BOOT (logo glitch-in, 3s, skippable)
  └─► TITLE  ──────────────────────────────► SETTINGS (overlay, any screen)
        │  [CONTINUE / NEW DIVE / SETTINGS / QUIT]
        ▼
   COMMAND HUB  ◄──────────────────────────────────────────────┐
   (base view = home screen; all programs launch from here)    │
        ├─► MISSION SELECT (sector map) ─► LOADOUT ─► BATTLE ──┤
        │                                              │       │
        │                                   PAUSE (overlay)    │
        │                                              │       │
        │                                          RESULTS ────┘
        ├─► TECH TREE                                  (rewards animate → hub)
        ├─► HEROES ─► HERO DETAIL (skill tree)
        ├─► ARMORY (units/blueprints/inventory)
        ├─► BASE BUILD MODE (upgrade buildings in-situ)
        ├─► THE BREACH (endless, unlocks post-campaign)
        └─► CODEX (lore, enemy intel, stats)
```

Rules:
- **Hub is home.** Every loop returns there; it visually changes as the base levels (Pillar 4 made visible).
- Max depth 3 from hub to any action. `Esc`/`B` always goes back one level; long-press = straight to hub.
- All transitions are themed: programs *boot* (window materializes, 200ms, scanline wipe) and *close* (collapse to line, 120ms). Battle entry is the big one: hub → "DIVE" → 1.2s jack-in tunnel sequence (also masks loading).

---

# 2. UI DESIGN SYSTEM — "ARC//OS"

## 2.1 Tokens (single source of truth — `UIThemeConfig` ScriptableObject)
| Token | Value | Use |
|---|---|---|
| `bg/void` | #05070F | screen background |
| `bg/panel` | #0B1220 @ 88% | window bodies, blurred backdrop |
| `stroke/holo` | #00E5FF @ 35% | 1px panel borders, dividers |
| `primary` | #00E5FF | interactive, focus, player force |
| `secondary` | #B026FF | rare/quantum, hero accents |
| `accent` | #FF2E97 | ultimates, highlights, "new!" |
| `success` | #00FF9C | affordable, complete, buffs |
| `warning` | #FFB300 | data/credits, caution |
| `danger` | #FF3D2E | enemy, errors, insufficient |
| `text/hi` | #E8F4FF | headings, values |
| `text/lo` | #7A8CA6 | labels, descriptions |
| Type: display | Orbitron (wide, techy) | titles, big numbers |
| Type: body | Rajdhani / Exo 2 | descriptions, buttons |
| Type: mono | JetBrains Mono | stats, damage numbers, code-flavor text |
| Radius | 2px outer, 45° corner-cuts on featured panels | the "chamfered hex" signature |
| Grid | 8px spacing system; 4px in dense HUD | |

## 2.2 Components (UI Toolkit/uGUI prefab library)
- **HoloPanel** — body blur + border + corner brackets `⌐ ¬` + animated scanline; variants: window / card / tooltip.
- **NeonButton** — states: idle (dim glow) / hover (bright +1.04×, 80ms) / press (0.96×) / disabled (desaturated, no glow) / confirm (ring burst). Sound per state from SoundTheme.
- **UnitCard** — portrait, cost chips (icon+number, each chip independently red when unaffordable), cooldown radial sweep, rank pips, drag ghost.
- **ResourceChip** — icon + animated count-up number; receives particle trails; pulses on income.
- **ProgressRing / Bar** — segmented, with overcharge state (ultimate ring flashes when full).
- **TooltipHolo** — 150ms delay, follows focus, shows full numbers + before/after deltas (UX rule: no hidden math).
- **TabStrip, Toggle, Slider, Dropdown, Modal, Toast** — themed equivalents; toasts slide from top-right, queue, never overlap battle HUD center.
- **GlitchText** — decode-in effect for titles (scramble → resolve, 300ms).
All components animate via central `MotionConfig` (doc 01 §4.3). Controller focus = visible bracket reticle, never invisible.

## 2.3 Layout principles
- Battle HUD edges only; center 60% of battlefield is sacred (gameplay readability).
- One primary action per screen, always bottom-right, always `A`/`Enter`.
- Numbers count up/down (200ms), never snap. Currency spends fly particles *to* the purchased thing.

---

# 3. WIREFRAMES (key screens)

## 3.1 BATTLE (the product)
```
┌──────────────────────────────────────────────────────────────┐
│ ⌐ CORE ████████░░ ¬   WAVE 3/7   ⌐ ENEMY CORE ███░░░░ ¬      │ ← top strip
│                                                              │
│   [YOUR        ✦ air lane ✈      ✈ enemy air     [ENEMY      │
│    NEXUS]   ▣▣▣  ground rows →  ← ▨▨▨            CORE]      │ 55%
│    ◉hero    far/mid/near parallax, city skyline bg           │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│ ◉NOVA  [ULT ◔ 68%] │ ⚡240 ◎85 ▲60 ◆12 ❖34 │ COMBO ×4 ▓▓▓░  │ ← command strip
│ [Drone][Troop][Mech][Titan][Tank][Intcp][Bombr][Hackr]      │   unit cards 1–8
├──────────────────────────────────────────────────────────────┤
│        8×8 COMPILER BOARD — holographic frame,               │
│        gems with per-type silhouettes, special tiles glow    │ 45%
│        (corner: reshuffle indicator · multiplier zone)       │
└──────────────────────────────────────────────────────────────┘
```
Notes: hero portrait pulses when active is affordable; ultimate ring flashes + audio cue at 100%; combo meter sits between the two screens deliberately — it's fed by the board and discharges onto the battlefield. Protocol draft: battle drops to 0.2× speed, 3 HoloPanels slide up over the board (board input locked, battlefield visible — tension preserved).

## 3.2 COMMAND HUB
```
┌──────────────────────────────────────────────────────────────┐
│ ARC//OS v9.7      ⚡credits ❖data ◆cores ▤blueprints    [⚙] │
│                                                              │
│        ISOMETRIC BASE VIEW (animated, lights, drones)        │
│   [Reactor]   [AI Lab]   [Drone Factory]                     │
│        [Quantum Forge]   [Defense Grid]    ← click = upgrade │
│                                                              │
│ ├ DIVE ▶ (primary, pulsing)                                  │
│ ├ TECH TREE   ├ HEROES   ├ ARMORY   ├ THE BREACH   ├ CODEX  │ ← program dock
│ "WARDEN's sector firewall weakening. Recommend dive." (HELIOS intel ticker)
└──────────────────────────────────────────────────────────────┘
```
Buildings visibly evolve with level (extra floors, more lights, drone traffic). New-unlock badges ping dock icons.

## 3.3 MISSION SELECT (sector map)
```
┌──────────────────────────────────────────────────────────────┐
│ ◄ SECTOR 2 / NEON DISTRICT ►            sector boss: MIRROR  │
│   ◉━━◉━━◉━━◈━━◉━━◉━━◈━━◉━━◉━━◉━━◉━━◉━━☠                     │
│   node = mission: ★★☆ grade, modifiers icons, reward preview │
│ ┌ SELECTED: 2-04 "Signal Hunt" ───────────────┐              │
│ │ brief (2 lines) · enemy comp icons · threat ▲▲△            │
│ │ first-clear: 40❖ +blueprint │ replay: 15❖   [ DEPLOY ▶ ]  │
│ └──────────────────────────────────────────────┘             │
└──────────────────────────────────────────────────────────────┘
```

## 3.4 LOADOUT (pre-battle)
Hero select (4 portraits, ult preview video on hover) · 8 unit slots from owned roster (class-filter tabs) · Stable Protocol slot · difficulty selector · enemy intel panel (composition revealed if Codex-scanned). One screen, `DEPLOY` bottom-right.

## 3.5 TECH TREE
Full-screen constellation: 5 branches radiating from center core (Warfare ⚔ / Aegis 🛡 / Flux ⚡ / Ascendancy ◉ / Infrastructure ▣). Nodes = hexes: locked (dim) / available (pulsing ring) / owned (filled, circuit lights up the connecting trace — purchases literally grow the network glow). Pan/zoom; node tooltip shows exact effect + cost; bottom bar: Data balance + respec.

## 3.6 HERO DETAIL
Left: full-body animated hero on holo-plinth (idle loop, taps trigger barks). Right: level/XP bar, 3-branch skill tree (18 nodes), ultimate showcase (PLAY button = full ult VFX in a vignette), stat deltas. Bottom: equip Stable Protocol synergy hints.

## 3.7 RESULTS
Sequence (skippable as a whole, 6s): grade stamp (★ slam, one per beat) → stat tape (damage, peak combo, MVP unit) → reward shower (currencies fly to header counters) → level-up toasts (hero XP bar fills, "+1 NODE") → [REPLAY] [NEXT MISSION ▶] [HUB]. Defeat variant: cause-of-death analytics panel (damage-by-source chart, income graph) + "WARDEN: Your inefficiency is noted." taunt + partial rewards (Pillar 4: defeat still pays).

## 3.8 SETTINGS (overlay)
Tabs: Video (display, quality, post toggles) / Audio (4 buses + **SoundTheme picker** with live preview) / Gameplay (difficulty, assists, auto-pause on draft) / Accessibility (colorblind shapes ON by default, shake/flash sliders, photosensitivity mode, UI scale) / Controls (full remap, M+KB / pad). Every slider previews live.

## 3.9 DEBUG PANEL (dev builds; `F1`)
Drawer with tabs: **Cheats** (resources, god mode, instant ult) · **Spawner** (any unit either team, elites, bosses) · **Director** (wave budget editor, pause AI) · **Time** (0.1×–8×) · **Levels** (jump to any mission/state) · **Battle Gen** (seeded random test battle) · **Tuning** (live MotionConfig/feel sliders, balance SO hot-reload) · **Telemetry** (income/DPS graphs overlay). Built on the same component library — dogfoods the UI kit.

---

# 4. UX FLOWS (critical paths)

- **First 10 minutes:** Boot → Title → auto-into Mission 1-01 (no hub yet; hub *unlocks* after first victory as a reward — teaches loop before meta) → guided board (3 forced matches) → guided deploy → win → hub reveal cinematic ("your hideout") → tutorial mission 2 teaches protocols, 3 teaches hero ultimate.
- **A purchase anywhere:** select → tooltip with before/after → confirm (hold 250ms on destructive/respec) → currency particles fly → thing visibly changes (building grows / node lights / card rank pip) → toast.
- **Pause in battle:** instant (real pause), blur battlefield+board, options: resume / settings / restart mission / abandon (hold). No stats shown that would enable pause-cheesing on Veteran+ (board state hidden behind blur).
