# NEON WARFARE — Steam Metagame v2 (Phase 7)
**Document 15 · 2026-06-12 · profile, cosmetics, achievements, seasonal · supersedes doc 08 metagame sections**

**Iron law: zero MTX, permanently.** Every item below is earned by play. Cosmetics never alter readability or power. No FOMO: time-limited art rotates into an earnable archive after its season.

---

## 1. Commander profile

- **Commander Level 1–100:** XP from any battle (win 100 × difficulty mult · loss 60 · raid defense 80). Curve front-loaded (level chime every 1–2 sessions early, every 3–4 late). Levels pay **cosmetics only**.
- **Profile card** (shown on victory/defeat, map corner, Steam-rich-presence): banner + title + commander level + 3 pinned achievements + showcased unit skin.
- **Stats vault:** lifetime de-rezzes, pylons flipped, EMPs fired, favorite lane, win % per faction, best Breach depth. Players quote stats; stats are free content.

## 2. Cosmetic catalog at 1.0

| Track | Count | What it skins | Sources |
|---|---|---|---|
| **Banners** | 25 | profile card + your Nexus flag in-battle | region clears (8), achievements (10), levels (5), seasonal archive (2) |
| **Titles** | 30 | profile card text | achievements (24), campaign (4), Breach percentile (2) |
| **Unit skins** | 24 (2/unit) | palette+trim variant; silhouette & class-trim color locked | territory 3★ (12), faction mastery (6), levels (6) |
| **Tile skin sets** | 6 | full gem set re-theme (silhouettes locked) | keystones (2), seasonal (2), achievements (1), default (1) |
| **Battlefield skins** | 5 | lane/backdrop theme per region aesthetic | region 100% (★★★ all 4 territories) |
| **Sound themes** | 2 | full SFX/music re-theme (*Neon*, *Retro Arcade*) | default + campaign clear |
| **Victory flourishes** | 6 | core-destruction sequence variant | Blackwall clears, Breach milestones |

Catalog rules:
- **Readability lock:** skins recolor within team-temperature bands (player stays cool-spectrum, enemy hot). QA gate: every skin passes the muted/blurred spectator test (doc 13 §5 acceptance).
- **Preview everything:** any locked cosmetic inspectable with its exact unlock condition. No mystery boxes — not even free ones. Surprise is for gameplay; respect is for the catalog.
- Equip slots: per-unit skin, one tile set, battlefield skin per region (or "match region" auto), banner, title, flourish.

## 3. Achievements (~40, mirrored to Steam)

### Progression spine (12)
Liberate each region (5) · beat each faction boss (3) · campaign clear · Veteran clear · Blackwall clear · 100% map stars.

### Mastery (16) — each is a build experiment, not a grind
- *Switchboard:* win controlling all 4 ground lanes simultaneously
- *Conductor:* fire 3 EMPs in one match
- *Stonewall:* win a raid defense without losing a pylon
- *Monoculture:* win using only Assault units
- *Ground Game:* win without deploying air (on a non-air-twist territory)
- *Sniper Economy:* win spending <200 total resources
- *Speedrunner:* any boss in <6 min
- *Counterintelligence:* beat Hall of Mirrors deploying ≤2 unit types
- *Lights Out:* kill 10 units with one Singularity-laser combo
- *Pacifist Reactor:* reach combo ×8 before your first deploy
- *Houdini:* win after your core hits <10%
- *Architect:* have 6 turrets alive at once
- *Air Traffic Control:* hold air superiority for an entire act
- *Tidal Wave:* flip 4 pylons within 30 s
- *Untouchable:* win with 100% core (Veteran+)
- *Deep Breach:* reach Breach depth 10

### Delight (12) — long-tail, found not farmed
*10,000 lifetime de-rezzes · watch the full idle animation of every core tier · win a match in every battlefield skin · trigger a double-Singularity board wipe · let a virus tile spread to 8 then clear it in one laser · …*

No achievement requires losing, idling, or playing badly. Anti-achievement test: "would a player be annoyed to see this pop?" 

## 4. Seasonal challenges (free, light-touch, post-1.0 pipeline)

- **Weekly Breach Seed:** endless mode, fixed seed + 2 stacked modifiers (e.g. *all drones* + *virus rain*). Friends + global leaderboard (depth). Top 10% = seasonal banner variant; everyone completing depth 5 = the season's tile-skin progress token.
- **Monthly Gauntlet:** 3 remixed territories (new twist combos on existing maps — content-cheap). Clearing all 3 = 1 seasonal cosmetic.
- **Season = 3 months.** Its cosmetics enter the **Archive** (earnable via gauntlet replays) one season later. Nothing is ever lost; only the *first-earn* moment is timed.
- Pipeline cost discipline: seasons are config (seeds, modifiers, reward pointers), never new systems. One designer-day per month, max.

## 5. Steam platform integration
- Achievements + stats API (the vault, §1) · Steam Cloud saves (versioned, doc 16 §6) · Rich presence ("Liberating the Foundry — Act II") · trading cards at 1.0 · Deck verified is a launch gate, not a patch.
- **No leaderboard MTX adjacents:** no paid name colors, ever. The Breach board shows banner+title — cosmetics are the flex, play is the price.

## 6. Why this retains without exploiting (design intent)
Retention levers, in honesty order: campaign pull (content) → stars/grades (mastery) → cosmetics (expression) → weekly seed (ritual). Every lever is *pull*, none is *push* — no daily login bait, no streak loss, no expiring power. A player who leaves for three months returns to find everything where they left it plus an archive to raid. That is what "premium means respect" costs and buys.
