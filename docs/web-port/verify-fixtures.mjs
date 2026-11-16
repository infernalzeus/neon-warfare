// Executable specification + self-check for docs/web-port/fixtures.
//
//   node docs/web-port/verify-fixtures.mjs
//
// Exports the exact reference implementations the TypeScript port needs for the pieces that are NOT the
// sim itself: the xorshift128 Rng, the FNV-1a state hash, daily seed, score/rank formulas, username keys.
// Running it proves (a) the fixtures are self-consistent (hashes recompute from the snapshots they ship
// with, so the float text format is lossless), and (b) these reference functions match the C# output.
// The sim port (CombatSim, BoardModel, BattleSession) is NOT here: that is the port's job, and it is done
// when it reproduces the per-tick / per-step hashes and event logs in the fixtures exactly.

import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const dir = join(here, 'fixtures');
const load = (f) => JSON.parse(readFileSync(join(dir, f), 'utf8'));

// ---- float32 helpers -------------------------------------------------------------------------------
const f32 = new Float32Array(1);
const i32 = new Int32Array(f32.buffer);
/** IEEE-754 binary32 bit pattern of x (x is rounded to float32 first, like C# `float`). */
export const bits = (x) => { f32[0] = x; return i32[0]; };
export const fround = Math.fround;

// ---- Rng: xorshift128 (Rng.cs) ---------------------------------------------------------------------
export class Rng {
  constructor(seed) {
    const s = seed >>> 0;
    this.x = (s ^ 0x9e3779b9) >>> 0;
    this.y = ((s << 13) | 1) >>> 0;
    this.z = (Math.imul(s, 0x85ebca6b) + 0xc2b2ae35) >>> 0;
    this.w = ((~s) | 1) >>> 0;
    for (let i = 0; i < 8; i++) this.nextUInt();
  }
  nextUInt() {
    const t = (this.x ^ (this.x << 11)) >>> 0;
    this.x = this.y; this.y = this.z; this.z = this.w;
    this.w = (this.w ^ (this.w >>> 19) ^ (t ^ (t >>> 8))) >>> 0;
    return this.w;
  }
  next(maxExclusive) { return maxExclusive <= 0 ? 0 : this.nextUInt() % maxExclusive; }
  range(min, maxExclusive) { return min + this.next(maxExclusive - min); }
  next01() { return (this.nextUInt() & 0xffffff) / 16777216; } // exactly representable in float32
}

// ---- state hash: FNV-1a over 32-bit words ----------------------------------------------------------
export class Hasher {
  constructor() { this.h = 2166136261; }
  w(n) { this.h = Math.imul(this.h ^ (n | 0), 16777619) >>> 0; }
  f(x) { this.w(bits(x)); }
}
const lo32 = (n) => Number(BigInt(n) & 0xffffffffn) | 0;

/** Word order is the contract. Mirrors WebFixtureExporter.CombatHash exactly. */
export function combatHashFromSnapshot(s) {
  const h = new Hasher();
  h.w(s.tick); h.w(s.act); h.w(s.surge); h.w(s.tele);
  h.f(s.teleRem); h.f(s.time); h.f(s.pCore); h.f(s.eCore); h.f(s.bank);
  h.w(lo32(s.nextId));
  h.w(s.finished); h.w(s.winner);
  for (const v of s.frontline) h.f(v);
  for (const [owner, progress, contested] of s.pylons) { h.w(owner); h.f(progress); h.w(contested); }
  h.w(s.units.length);
  for (const [alive, id, spec, team, lane, x, hp, cd, stun, slow, target] of s.units) {
    h.w(alive); h.w(lo32(id)); h.w(spec); h.w(team); h.w(lane);
    h.f(x); h.f(hp); h.f(cd); h.f(stun); h.f(slow); h.w(target);
  }
  return h.h;
}

/** Board hash over a grid dump {cols,rows,gems:[row strings],specials:[row strings]}. Column-major order. */
export function boardHashFromDump(g) {
  const h = new Hasher();
  for (let x = 0; x < g.cols; x++) {
    for (let y = 0; y < g.rows; y++) {
      const ch = g.gems[y][x];
      h.w(ch === '.' ? 0 : ch.charCodeAt(0) - 48);
      h.w(g.specials[y].charCodeAt(x) - 48);
      h.w(ch === '.' ? 1 : 0);
    }
  }
  return h.h;
}

// ---- formulas --------------------------------------------------------------------------------------
export const dailySeed = (y, m, d, level) => (((y * 10000 + m * 100 + d) * 31 + level) & 0x7fffffff);

/** C# Math.Round(double): ties go to the even integer (NOT Math.round). */
export function roundHalfEven(x) {
  const r = Math.round(x);
  return Math.abs(x % 1) === 0.5 && r % 2 !== 0 ? r - 1 : r;
}
/** GhostRecorder.Finish: stored speed + scoreTicks. */
export function scoreTicks(durationTicks, speedIn) {
  const speed = fround(speedIn <= fround(0.05) ? 1 : speedIn);
  return { speed, scoreTicks: Math.max(1, roundHalfEven(durationTicks / speed)) };
}
export const sortKey = (won, ticks) => (won ? 1e9 : 0) + (won ? 1e9 - ticks : ticks);

export function eloDelta(won, my, opp) {
  const expected = 1 / (1 + Math.pow(10, (opp - my) / 400));
  const raw = 32 * ((won ? 1 : 0) - expected);
  const d = roundHalfEven(raw);
  const mag = Math.min(40, Math.max(6, Math.abs(d)));
  return won ? mag : -mag;
}
const TIERS = [[0, 'BRONZE'], [1000, 'SILVER'], [1200, 'GOLD'], [1400, 'PLATINUM'], [1600, 'DIAMOND'], [1800, 'APEX']];
export function tier(mmr) {
  let name = TIERS[0][1];
  for (const [floor, n] of TIERS) if (mmr >= floor) name = n;
  const nextTierAt = (TIERS.find(([f]) => mmr < f) ?? TIERS[TIERS.length - 1])[0];
  let progress = 0;
  for (let i = 0; i < TIERS.length; i++) {
    if (mmr < TIERS[i][0]) continue;
    if (i === TIERS.length - 1) { progress = 1; break; }
    const lo = TIERS[i][0], hi = TIERS[i + 1][0];
    if (mmr < hi) { progress = Math.min(1, Math.max(0, fround((mmr - lo) / (hi - lo)))); break; }
  }
  return { name, progress, nextTierAt };
}

// UsernameService.Normalize + KeyOf. C# works on UTF-16 code units: surrogate halves are neither
// letters nor digits, so emoji and other astral characters are dropped. Iterate code units, not code points.
const isLetterOrDigit = (c) => /^[\p{L}\p{Nd}]$/u.test(c);
const isWhiteSpace = (c) => /^[\p{Zs}\p{Zl}\p{Zp}\t\n\v\f\r\u0085]$/u.test(c);
export function normalizeUsername(raw) {
  if (!raw) return '';
  let s = raw, a = 0, b = s.length;
  while (a < b && isWhiteSpace(s[a])) a++;
  while (b > a && isWhiteSpace(s[b - 1])) b--;
  let out = '', lastWasSpace = false;
  for (let i = a; i < b; i++) {
    const c = s[i];
    if (isLetterOrDigit(c) || c === '_' || c === '-') { out += c; lastWasSpace = false; }
    else if (isWhiteSpace(c) && !lastWasSpace && out.length > 0) { out += ' '; lastWasSpace = true; }
  }
  let e = out.length;
  while (e > 0 && isWhiteSpace(out[e - 1])) e--;
  return out.slice(0, e);
}
// .NET ToLowerInvariant is a 1:1 simple case mapping per UTF-16 unit; JS toLowerCase can expand one unit
// into several. Keep only single-unit results, else leave the character unchanged.
export function lowerInvariant(s) {
  let out = '';
  for (let i = 0; i < s.length; i++) { const l = s[i].toLowerCase(); out += l.length === 1 ? l : s[i]; }
  return out;
}
export const usernameKey = (raw) => lowerInvariant(normalizeUsername(raw)).replaceAll(' ', '_');
export const usernameValid = (raw) => { const n = normalizeUsername(raw).length; return n >= 3 && n <= 16; };

// ---- self-check ------------------------------------------------------------------------------------
function main() {
  let fails = 0, checks = 0;
  const eq = (what, a, b) => {
    checks++;
    if (a !== b) { fails++; if (fails <= 25) console.log(`MISMATCH ${what}: got ${a} expected ${b}`); }
  };

  const rng = load('rng.json');
  for (const c of rng.cases) {
    let r = new Rng(c.seed);
    c.nextUInt.forEach((v, i) => eq(`rng seed ${c.seed} nextUInt[${i}]`, r.nextUInt(), v));
    r = new Rng(c.seed); c.next7.forEach((v, i) => eq(`rng ${c.seed} next7[${i}]`, r.next(7), v));
    r = new Rng(c.seed); c.range3to9.forEach((v, i) => eq(`rng ${c.seed} range[${i}]`, r.range(3, 9), v));
    r = new Rng(c.seed); c.next01.forEach((v, i) => eq(`rng ${c.seed} next01[${i}]`, bits(r.next01()), bits(v)));
    eq(`rng ${c.seed} next(0)`, new Rng(c.seed).next(0), c.next0);
  }

  const fx = load('formulas.json');
  for (const d of fx.dailySeed) eq(`dailySeed ${d.y}-${d.m}-${d.d} L${d.level}`, dailySeed(d.y, d.m, d.d, d.level), d.seed);
  for (const d of fx.sortKey) eq(`sortKey ${d.won} ${d.ticks}`, sortKey(!!d.won, d.ticks), d.key);
  for (const d of fx.scoreTicks) {
    const r = scoreTicks(d.duration, fround(d.speedIn));
    eq(`scoreTicks ${d.duration}@${d.speedIn}`, r.scoreTicks, d.scoreTicks);
    eq(`speedStored ${d.speedIn}`, bits(r.speed), bits(d.speedStored));
  }
  for (const d of fx.eloDelta) eq(`elo ${d.won} ${d.my} ${d.opp}`, eloDelta(!!d.won, d.my, d.opp), d.delta);
  for (const d of fx.tiers) {
    const t = tier(d.mmr);
    eq(`tier ${d.mmr}`, t.name, d.tier); eq(`tierProgress ${d.mmr}`, bits(t.progress), bits(d.progress)); eq(`nextTier ${d.mmr}`, t.nextTierAt, d.next);
  }
  for (const d of fx.usernameKey) {
    eq(`normalize ${JSON.stringify(d.raw)}`, normalizeUsername(d.raw), d.normalized);
    eq(`key ${JSON.stringify(d.raw)}`, usernameKey(d.raw), d.key);
    eq(`valid ${JSON.stringify(d.raw)}`, usernameValid(d.raw), !!d.valid);
  }

  let combatFiles = 0, snapshots = 0, boardFiles = 0, sessionFiles = 0;
  for (const f of readdirSync(dir)) {
    if (f.startsWith('combat_')) {
      combatFiles++;
      const c = load(f);
      for (const s of c.snapshots) {
        snapshots++;
        const idx = Math.min(s.tick, c.hashEveryTickFrom1.length) - 1;
        if (idx < 0) continue;
        eq(`${f} snapshot tick ${s.tick} hash`, combatHashFromSnapshot(s), c.hashEveryTickFrom1[idx]);
      }
    } else if (f.startsWith('board_')) {
      boardFiles++;
      const b = load(f);
      eq(`${f} initialHash`, boardHashFromDump(b.initial), b.initialHash);
      eq(`${f} finalHash`, boardHashFromDump(b.final), b.finalHash);
    } else if (f.startsWith('session_')) {
      sessionFiles++;
      const s = load(f);
      eq(`${f} has inputs`, Array.isArray(s.inputs), true);
      for (const sn of s.snapshots) {
        // Mirrors WebFixtureExporter.RunSession: combat hash word, resources, combos, card cooldowns, timers, board.
        const h = new Hasher();
        h.w(combatHashFromSnapshot(sn.combat) | 0);
        for (const r of sn.resources) h.w(r);
        h.w(sn.liveCombo); h.w(sn.peakCombo);
        for (const cd of sn.cards) h.f(cd);
        h.f(sn.decay); h.f(sn.pend);
        h.w(boardHashFromDump(sn.board) | 0);
        eq(`${f} step ${sn.step} hash`, h.h, s.hashEveryStepFrom1[sn.step - 1]);
      }
    }
  }
  console.log(`checked ${checks} values; ${combatFiles} combat files (${snapshots} snapshots), ${boardFiles} board files, ${sessionFiles} session files`);
  if (fails) { console.log(`${fails} MISMATCHES`); process.exit(1); }
  console.log('OK: fixtures are self-consistent and the reference helpers match the C# output.');
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) main();
