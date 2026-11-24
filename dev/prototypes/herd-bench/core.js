// Herd split sim core, v3: caribou deciding like #83's food planner at the game's values on main (linear regrowth
// over 28 days, grazing down by mass, 5+ minutes a tile, bodies +/-15%), plus thirst and water, and one wolf pack.
// Exposed as HerdSim (browser) or module.exports (node).
(function (root) {
  function rng(seed) {
    let a = seed >>> 0;
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  const DEFAULTS = {
    W: 40, H: 26, seed: 7,
    terrain: 'valley',                   // 'valley' (river meadows, poor uplands) | 'meadows' (rich patches in poor ground) | 'uniform'
    contrast: 1.5,                       // >1 sharpens the gap between rich and poor ground
    animals: 40, start: 'game',          // 'game' (3 herds of 5-15) | 'herd' (N on one tile) | 'scattered'
    K: 7,                                // grazers a typical tile feeds at full rate
    capBody: true,                       // capacity counts body size (intake ~ mass^0.75), not heads
    capForage: 'fixed',                  // 'fixed' | 'rich' (K scales with the tile's richness) | 'now' (with forage left)
    kinRadius: 1,                        // kin count as present within this many tiles (0: same tile, as the spec)
    follow: true, arrivalReset: false, mealReplan: false, loneHourly: true,
    cohesion: 1, stayFactor: 1.25,
    amount: 180,                         // minutes of eating at the fresh rate that one meal is
    bodySpread: 0.15,                    // the game's: average of two uniforms, +/-15%
    noise: 0.05,                         // +/- jitter on each candidate's score
    tileMin: 5, radius: 10,
    massKg: 110, grazeRate: 0.0001,      // Fauna.GrazingRatePerKgPerHour
    regrowDays: 28,                      // Pools.ForageRespawnHours = 672
    // Water
    water: true, ponds: 4, drinkHours: 12, drinkMin: 10,
    // Wolves
    wolves: true, packSize: 3, huntDays: 2, wolfSense: 12, preySense: 6,
    watch: 0.04,                         // chance a minute that one caribou notices wolves at the edge of its sight
    ambush: 0.5,                         // a pack that reaches its quarry unseen kills it this often
    catchPerMin: 0.3, chaseLimit: 20, stalkMin: 4, chaseMin: 1, fleeMin: 1, fleeTiles: 6,
    avoid: 2,                            // sources this close to sensed wolves are refused
    arrivals: true,                      // a killed caribou is replaced by an arrival at the map's edge a day later
  };

  function valueNoise(R, W, H, cell) {
    const gw = Math.ceil(W / cell) + 2, gh = Math.ceil(H / cell) + 2, g = new Float64Array(gw * gh);
    for (let i = 0; i < g.length; i++) g[i] = R();
    const sm = t => t * t * (3 - 2 * t), out = new Float64Array(W * H);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const fx = x / cell, fy = y / cell, ix = Math.floor(fx), iy = Math.floor(fy), tx = sm(fx - ix), ty = sm(fy - iy);
      const v = (i, j) => g[(iy + j) * gw + ix + i];
      out[y * W + x] = (v(0, 0) * (1 - tx) + v(1, 0) * tx) * (1 - ty) + (v(0, 1) * (1 - tx) + v(1, 1) * tx) * ty;
    }
    return out;
  }

  function land(p, R, W, H) {
    const n = W * H, rich = new Float64Array(n), water = new Uint8Array(n);
    const big = valueNoise(R, W, H, 10), mid = valueNoise(R, W, H, 4), fine = valueNoise(R, W, H, 1.5);
    if (p.terrain === 'valley') {
      const phase = R() * 6.28, amp = H * 0.18, wave = W / (1.2 + R());
      const rock = valueNoise(R, W, H, 3);
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = y * W + x, river = H / 2 + amp * Math.sin(x / wave * 6.28 + phase);
        const d = Math.abs(y - river) / (H / 2);
        let v = 0.15 + 0.85 * Math.exp(-d * d * 4) * (0.6 + 0.4 * big[i]);
        v *= 0.75 + 0.25 * mid[i] + 0.1 * (fine[i] - 0.5);
        if (rock[i] > 0.78 && d > 0.25) v *= 0.15;
        rich[i] = v;
      }
      // The river as an unbroken line: each column joins the previous column's row.
      if (p.water) {
        const rowAt = x => Math.round(H / 2 + amp * Math.sin(x / wave * 6.28 + phase));
        for (let x = 0; x < W; x++) {
          const r0 = x ? rowAt(x - 1) : rowAt(x), r1 = rowAt(x);
          for (let y = Math.min(r0, r1); y <= Math.max(r0, r1); y++) if (y >= 0 && y < H) water[y * W + x] = 1;
        }
      }
    } else if (p.terrain === 'meadows') {
      const m = 5 + Math.floor(R() * 4), blobs = [];
      for (let k = 0; k < m; k++) blobs.push({ x: R() * W, y: R() * H, r: 1.5 + R() * 4.5, q: 0.5 + R() * 0.5 });
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = y * W + x; let v = 0.08 + 0.12 * big[i];
        for (const b of blobs) { const d2 = ((x - b.x) ** 2 + (y - b.y) ** 2) / (b.r * b.r); v = Math.max(v, b.q * Math.exp(-d2) * (0.8 + 0.2 * mid[i])); }
        rich[i] = v;
      }
    } else {
      for (let i = 0; i < n; i++) rich[i] = 0.6 * mid[i] + 0.4 * fine[i];
    }
    let lo = Infinity, hi = -Infinity;
    for (let i = 0; i < n; i++) { rich[i] = Math.pow(Math.max(0, rich[i]), p.contrast); lo = Math.min(lo, rich[i]); hi = Math.max(hi, rich[i]); }
    for (let i = 0; i < n; i++) rich[i] = 0.03 + 0.97 * (rich[i] - lo) / (hi - lo || 1);
    if (p.water) for (let k = 0; k < p.ponds; k++) {
      const cx = 2 + Math.floor(R() * (W - 4)), cy = 2 + Math.floor(R() * (H - 4)), cells = 1 + Math.floor(R() * 4);
      water[cy * W + cx] = 1;
      for (let c = 1; c < cells; c++) water[(cy + Math.floor(R() * 2)) * W + cx + Math.floor(R() * 2)] = 1;
    }
    for (let i = 0; i < n; i++) if (water[i]) rich[i] = 0;
    return { rich, water };
  }

  function newCaribou(s, id, tile) {
    const p = s.p, R = s.R;
    const size = Math.min(1.3, Math.max(0.7, 1 + ((R() + R()) / 2 - 0.5) * 2 * p.bodySpread));
    return {
      id, tile, target: tile, step: 0, stepMin: p.tileMin, mode: 'food', eating: false, planned: 0,
      mass: p.massKg * size, demand: Math.pow(size, 0.75), amount: p.amount * Math.pow(size, 0.75),
      thirst: Math.floor(R() * p.drinkHours * 60), drinking: 0,
      pending: null, last: null, eaten: 0, px: tile % s.W, py: Math.floor(tile / s.W),
    };
  }

  function create(opts) {
    const p = Object.assign({}, DEFAULTS, opts || {});
    const R = rng(p.seed);
    const W = p.W, H = p.H, n = W * H;
    const { rich, water } = land(p, R, W, H);
    let meanRich = 0, landTiles = 0;
    for (let i = 0; i < n; i++) if (!water[i]) { meanRich += rich[i]; landTiles++; }
    meanRich /= landTiles;
    const waterTiles = []; for (let i = 0; i < n; i++) if (water[i]) waterTiles.push(i);
    const s = {
      p, W, H, n, rich, meanRich, water, waterTiles, grazed: new Float64Array(n), agents: [], R, minute: 0, nextId: 0,
      on: new Int32Array(n), heading: new Int32Array(n), load: new Float64Array(n),
      log: [], carcasses: [], arrivalsDue: [], clusterOf: new Int32Array(n).fill(-1), clusterSize: [],
      stats: { moves: 0, hours: 0, samples: [], kills: [], hunts: 0, alarms: 0, drinks: 0 },
      wolves: null,
    };
    const byRich = [...rich.keys()].sort((a, b) => rich[b] - rich[a]);
    const starts = [];
    if (p.start === 'game') {
      for (let h = 0; h < 3; h++) {
        const tile = byRich[Math.floor(R() * Math.min(60, n))], size = 5 + Math.floor(R() * 11);
        for (let i = 0; i < size; i++) starts.push(tile);
      }
    } else {
      for (let i = 0; i < p.animals; i++) {
        let t = p.start === 'scattered' ? Math.floor(R() * n) : byRich[0];
        while (water[t]) t = Math.floor(R() * n);
        starts.push(t);
      }
    }
    for (const tile of starts) s.agents.push(newCaribou(s, s.nextId++, tile));
    if (p.wolves) {
      let t; do { t = Math.floor(R() * n); } while (water[t] || Math.min(...s.agents.map(a => dist(s, a.tile, t))) < 10);
      s.wolves = { tile: t, target: t, step: 0, stepMin: p.tileMin, px: t % W, py: Math.floor(t / W), state: 'rest',
        hunger: Math.floor(R() * p.huntDays * 1440), quarry: -1, chaseT: 0, timer: 0, kills: 0 };
    }
    recount(s);
    clusterPass(s);
    for (const a of s.agents) decide(s, a, 'start');
    return s;
  }

  function recount(s) {
    s.on.fill(0); s.heading.fill(0); s.load.fill(0);
    for (const a of s.agents) { s.on[a.tile]++; s.load[a.tile] += weight(s, a); if (a.target !== a.tile) s.heading[a.target]++; }
  }
  const weight = (s, a) => (s.p.capBody ? a.demand : 1);
  const forageAt = (s, t) => s.rich[t] * (1 - s.grazed[t]);
  const dist = (s, a, b) => Math.max(Math.abs((a % s.W) - (b % s.W)), Math.abs(Math.floor(a / s.W) - Math.floor(b / s.W)));
  const byId = (s, id) => s.agents.find(a => a.id === id);

  function capacity(s, t) {
    const p = s.p;
    if (p.capForage === 'rich') return p.K * s.rich[t] / s.meanRich;
    if (p.capForage === 'now') return p.K * forageAt(s, t) / s.meanRich;
    return p.K;
  }
  function loadAt(s, a, t) { return s.load[t] - (a.tile === t ? weight(s, a) : 0) + weight(s, a); }
  function shareAt(s, a, t) { const L = loadAt(s, a, t), K = capacity(s, t); return L <= K ? 1 : K / L; }
  function kinAt(s, a, t) {
    const r = s.p.kinRadius, tx = t % s.W, ty = Math.floor(t / s.W);
    for (let y = Math.max(0, ty - r); y <= Math.min(s.H - 1, ty + r); y++)
      for (let x = Math.max(0, tx - r); x <= Math.min(s.W - 1, tx + r); x++) {
        const u = y * s.W + x;
        const on = s.on[u] - (a.tile === u ? 1 : 0), hd = s.heading[u] - (a.target === u && a.tile !== u ? 1 : 0);
        if (on + hd > 0) return true;
      }
    return false;
  }
  // Wolves this animal can see; a source near them is refused (spec: "caribou avoid a source a predator holds").
  const wolvesSeen = (s, a) => s.wolves && dist(s, a.tile, s.wolves.tile) <= s.p.preySense;
  const held = (s, a, t) => wolvesSeen(s, a) && dist(s, t, s.wolves.tile) <= s.p.avoid;

  function forecast(s, a, t) {
    const share = shareAt(s, a, t);
    const rate = s.water[t] ? 0 : forageAt(s, t) * share;
    const travel = dist(s, a.tile, t) * s.p.tileMin;
    const kin = kinAt(s, a, t);
    const eat = rate > 1e-9 ? a.amount / rate : Infinity;
    let total = held(s, a, t) ? Infinity : (travel + eat) * (kin ? 1 : 1 + s.p.cohesion);
    if (s.p.noise > 0) total *= 1 + s.p.noise * (2 * s.R() - 1);
    return { t, load: loadAt(s, a, t), K: capacity(s, t), rate, travel, kin, total };
  }
  function waterForecast(s, a, t) {
    const travel = dist(s, a.tile, t) * s.p.tileMin, kin = kinAt(s, a, t);
    let total = held(s, a, t) ? Infinity : (travel + s.p.drinkMin) * (kin ? 1 : 1 + s.p.cohesion);
    if (s.p.noise > 0) total *= 1 + s.p.noise * (2 * s.R() - 1);
    return { t, load: 0, K: 0, rate: 0, travel, kin, total, water: true };
  }

  function setPlan(s, a, t, rate, mode, stepMin) {
    if (a.target !== a.tile) s.heading[a.target]--;
    a.target = t; a.planned = rate; a.mode = mode; a.stepMin = stepMin || s.p.tileMin;
    if (t !== a.tile) { s.heading[t]++; a.eating = false; a.step = 0; a.eaten = 0; startStep(s, a); }
    else arrive(s, a);
  }
  function startStep(s, a) {
    const ax = a.tile % s.W, ay = Math.floor(a.tile / s.W), tx = a.target % s.W, ty = Math.floor(a.target / s.W);
    const next = (ay + Math.sign(ty - ay)) * s.W + ax + Math.sign(tx - ax);
    a.px = ax; a.py = ay;
    s.on[a.tile]--; s.load[a.tile] -= weight(s, a);
    a.tile = next; s.on[next]++; s.load[next] += weight(s, a);
    if (next === a.target) s.heading[a.target]--;
    a.step = a.stepMin; s.stats.moves++;
  }
  function arrive(s, a) {
    a.px = a.tile % s.W; a.py = Math.floor(a.tile / s.W);
    if (a.mode === 'water') { a.drinking = s.p.drinkMin; a.eating = false; return; }
    if (a.mode === 'flee') {
      const w = s.wolves;
      if (w && w.state === 'chase' && w.quarry === a.id && dist(s, a.tile, w.tile) <= 3) { const h = { tiles: [a.tile], ids: [a.id], count: 1 }; a.mode = 'food'; flee(s, h, 'still chased'); return; }
      a.mode = 'food'; decide(s, a, 'after flight'); return;
    }
    a.eating = true;
    if (s.p.arrivalReset) a.planned = forecast(s, a, a.tile).rate;
  }

  function decide(s, a, why) {
    s.stats.searches = (s.stats.searches || 0) + 1;
    const here = forecast(s, a, a.tile);
    let bestC = null; const cands = [];
    const ax = a.tile % s.W, ay = Math.floor(a.tile / s.W), r = s.p.radius;
    for (let y = Math.max(0, ay - r); y <= Math.min(s.H - 1, ay + r); y++)
      for (let x = Math.max(0, ax - r); x <= Math.min(s.W - 1, ax + r); x++) {
        const t = y * s.W + x; if (t === a.tile || s.water[t]) continue;
        const c = forecast(s, a, t); cands.push(c);
        if (!bestC || c.total < bestC.total || (c.total === bestC.total && t < bestC.t)) bestC = c;
      }
    cands.sort((u, v) => u.total - v.total);
    const go = bestC && bestC.total * s.p.stayFactor < here.total;
    a.last = { minute: s.minute, why, kind: 'search', here, cands: cands.slice(0, 5), chose: go ? bestC.t : a.tile };
    if (go) { const from = a.tile; setPlan(s, a, bestC.t, bestC.rate, 'food'); departed(s, a, from, why); }
    else { a.mode = 'food'; a.planned = here.rate; a.eating = a.target === a.tile; }
  }
  function planWater(s, a, why, only) {
    let best = null; const cands = [];
    for (const t of only != null ? [only] : s.waterTiles) {
      const c = waterForecast(s, a, t); cands.push(c);
      if (!best || c.total < best.total) best = c;
    }
    cands.sort((u, v) => u.total - v.total);
    if (!best || best.total === Infinity) return false;
    a.last = { minute: s.minute, why, kind: 'water', here: null, cands: cands.slice(0, 5), chose: best.t };
    const from = a.tile; setPlan(s, a, best.t, 0, 'water'); departed(s, a, from, why);
    return true;
  }
  // Kin left: go where they are going, for the same need, or stay.
  function reweigh(s, a, ev) {
    if (ev.mode === 'water') {
      if (a.thirst > 0.5 * s.p.drinkHours * 60) planWater(s, a, 'kin went to drink', ev.to);
      return;
    }
    const here = forecast(s, a, a.tile), there = forecast(s, a, ev.to);
    const go = there.total * s.p.stayFactor < here.total;
    a.last = { minute: s.minute, why: 'kin left', kind: 'follow', here, cands: [there], chose: go ? ev.to : a.tile };
    if (go) { const from = a.tile; setPlan(s, a, ev.to, there.rate, 'food'); departed(s, a, from, 'followed'); }
  }
  function departed(s, a, from, why) {
    logEvent(s, { kind: 'move', id: a.id, why, from, to: a.target, mode: a.mode });
    if (!s.p.follow) return;
    for (const b of s.agents)
      if (b !== a && b.tile === from && b.target === from && !b.pending && b.mode === 'food')
        b.pending = { to: a.target, mode: a.mode };
  }
  function logEvent(s, e) { e.minute = s.minute; s.log.push(e); if (s.log.length > 600) s.log.shift(); }

  // Herds as connected clusters of occupied tiles (8-neighbour).
  function clusters(s) {
    const g = new Map();
    for (const a of s.agents) { if (!g.has(a.tile)) g.set(a.tile, []); g.get(a.tile).push(a.id); }
    const seen = new Set(), out = [];
    for (const t of g.keys()) {
      if (seen.has(t)) continue;
      const stack = [t], tiles = [], ids = []; seen.add(t);
      while (stack.length) {
        const u = stack.pop(); tiles.push(u); ids.push(...g.get(u));
        const ux = u % s.W, uy = Math.floor(u / s.W);
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          const x = ux + dx, y = uy + dy; if (x < 0 || y < 0 || x >= s.W || y >= s.H) continue;
          const v = y * s.W + x; if (g.has(v) && !seen.has(v)) { seen.add(v); stack.push(v); }
        }
      }
      out.push({ tiles, ids, count: ids.length });
    }
    return out.sort((a, b) => b.count - a.count);
  }
  function clusterPass(s) {
    s.clusterOf.fill(-1); s.clusterList = clusters(s);
    s.clusterList.forEach((c, i) => { for (const t of c.tiles) s.clusterOf[t] = i; });
  }
  const herdOf = (s, a) => s.clusterList[s.clusterOf[a.tile]];

  // Every animal in the herd runs, away from the wolves, in roughly the same direction.
  function flee(s, herd, why) {
    const w = s.wolves, wx = w.tile % s.W, wy = Math.floor(w.tile / s.W);
    let cx = 0, cy = 0; for (const t of herd.tiles) { cx += t % s.W; cy += Math.floor(t / s.W); }
    cx /= herd.tiles.length; cy /= herd.tiles.length;
    const base = Math.atan2(cy - wy, cx - wx) + (s.R() - 0.5) * 0.8;
    s.stats.alarms++;
    logEvent(s, { kind: 'alarm', count: herd.count, d: dist(s, herd.tiles[0], w.tile), why });
    for (const id of herd.ids) {
      const a = byId(s, id); if (!a || a.mode === 'flee') continue;
      const ang = base + (s.R() - 0.5) * 0.9, ax = a.tile % s.W, ay = Math.floor(a.tile / s.W);
      let tx = Math.round(ax + Math.cos(ang) * s.p.fleeTiles), ty = Math.round(ay + Math.sin(ang) * s.p.fleeTiles);
      tx = Math.max(0, Math.min(s.W - 1, tx)); ty = Math.max(0, Math.min(s.H - 1, ty));
      let to = ty * s.W + tx;
      if (dist(s, to, w.tile) <= dist(s, a.tile, w.tile)) {
        // Pinned against the map's edge: run along it to whichever neighbour is farthest from the wolves, if any.
        let best = a.tile;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          const x = ax + dx, y = ay + dy; if (x < 0 || y < 0 || x >= s.W || y >= s.H) continue;
          const v = y * s.W + x; if (dist(s, v, w.tile) > dist(s, best, w.tile)) best = v;
        }
        to = best;
      }
      if (to === a.tile) continue;   // cornered
      a.pending = null; a.drinking = 0;
      setPlan(s, a, to, 0, 'flee', s.p.fleeMin);
    }
  }

  function kill(s, a) {
    const w = s.wolves, herd = herdOf(s, a), n = herd ? herd.count : 1;
    s.on[a.tile]--; s.load[a.tile] -= weight(s, a); if (a.target !== a.tile) s.heading[a.target]--;
    s.agents.splice(s.agents.indexOf(a), 1);
    s.carcasses.push({ tile: a.tile, minute: s.minute });
    s.stats.kills.push({ minute: s.minute, herd: n, mass: a.mass });
    w.kills++; w.hunger = 0; w.state = 'feed'; w.timer = 8 * 60; w.quarry = -1; w.target = w.tile;
    logEvent(s, { kind: 'kill', id: a.id, herd: n, tile: a.tile });
    if (s.p.arrivals) s.arrivalsDue.push(s.minute + 1440);
    const h = herd; if (h) flee(s, { tiles: h.tiles, ids: h.ids.filter(id => id !== a.id), count: n - 1 }, 'kill');
  }

  function wolfStep(s, w, to, stepMin) {
    w.target = to; w.stepMin = stepMin;
    if (w.step > 0 || w.tile === to) return;
    const ax = w.tile % s.W, ay = Math.floor(w.tile / s.W), tx = to % s.W, ty = Math.floor(to / s.W);
    w.px = ax; w.py = ay; w.tile = (ay + Math.sign(ty - ay)) * s.W + ax + Math.sign(tx - ax); w.step = stepMin;
  }
  function wolfTick(s) {
    const w = s.wolves, p = s.p;
    if (w.step > 0) { w.step--; if (w.step <= 0) { w.px = w.tile % s.W; w.py = Math.floor(w.tile / s.W); } }
    w.hunger++;
    if (w.state === 'feed' || w.state === 'rest') {
      if (--w.timer > 0) return;
      if (w.state === 'feed') { w.state = 'rest'; w.timer = 4 * 60; return; }
      w.state = w.hunger >= p.huntDays * 1440 ? 'search' : 'rest'; w.timer = 60;
      if (w.state === 'rest') return;
    }
    if (w.state === 'search') {
      let best = null, bs = Infinity;
      for (const a of s.agents) {
        const d = dist(s, w.tile, a.tile); if (d > p.wolfSense) continue;
        const h = herdOf(s, a), sc = d * (1 + 0.15 * ((h ? h.count : 1) - 1));
        if (sc < bs) { bs = sc; best = a; }
      }
      if (best) { w.state = 'stalk'; w.quarry = best.id; s.stats.hunts++; logEvent(s, { kind: 'hunt', id: best.id, herd: herdOf(s, best).count }); }
      else if (w.step <= 0) {
        if (w.tile === w.target) w.target = Math.max(0, Math.min(s.n - 1, w.tile + (Math.floor(s.R() * 9) - 4) + (Math.floor(s.R() * 9) - 4) * s.W));
        wolfStep(s, w, w.target, p.tileMin);
      }
      return;
    }
    const q = byId(s, w.quarry);
    if (!q) { w.state = 'search'; return; }
    if (w.state === 'stalk') {
      if (q.mode === 'flee') { w.state = 'chase'; w.chaseT = 0; return; }
      if (dist(s, w.tile, q.tile) <= 1 && w.step <= 0) {
        if (s.R() < p.ambush) { kill(s, q); return; }
        w.state = 'chase'; w.chaseT = 0; const h = herdOf(s, q); if (h) flee(s, h, 'ambush failed'); return;
      }
      wolfStep(s, w, q.tile, p.stalkMin);
      return;
    }
    if (w.state === 'chase') {
      if (++w.chaseT > p.chaseLimit) {
        logEvent(s, { kind: 'gaveup', id: q.id }); w.state = 'rest'; w.timer = 6 * 60; w.quarry = -1; return;
      }
      if (w.tile === q.tile && s.R() < p.catchPerMin) { kill(s, q); return; }
      wolfStep(s, w, q.tile, p.chaseMin);
    }
  }

  // Each caribou that can see hunting wolves has a chance to notice them, higher the closer they are;
  // one that notices sets its whole herd running. More eyes, earlier warning.
  function watchPass(s) {
    const w = s.wolves; if (!w || !(w.state === 'stalk' || w.state === 'chase')) return;
    const done = new Set();
    for (const a of s.agents) {
      if (a.mode === 'flee') continue;
      const d = dist(s, a.tile, w.tile); if (d > (w.state === 'chase' ? 3 : s.p.preySense)) continue;
      const ci = s.clusterOf[a.tile]; if (done.has(ci)) continue;
      // A running pack is seen at once; a stalking one only by luck and eyes.
      const chance = w.state === 'chase' ? 1 : s.p.watch * (1 - d / (s.p.preySense + 1));
      if (s.R() < chance) { done.add(ci); flee(s, s.clusterList[ci], 'spotted wolves'); }
    }
  }

  function tick(s) {
    const p = s.p, regrow = 1 / (p.regrowDays * 1440);
    for (let t = 0; t < s.n; t++) if (s.grazed[t] > 0) s.grazed[t] = Math.max(0, s.grazed[t] - regrow);
    clusterPass(s);
    if (s.wolves) { watchPass(s); wolfTick(s); }
    for (const a of [...s.agents]) {
      if (!s.agents.includes(a)) continue;
      a.thirst++;
      if (a.tile !== a.target || a.step > 0) {
        a.step--;
        if (a.step <= 0) { if (a.tile === a.target) arrive(s, a); else startStep(s, a); }
        continue;
      }
      if (a.drinking > 0) {
        if (--a.drinking === 0) { a.thirst = 0; s.stats.drinks++; a.mode = 'food'; decide(s, a, 'drank'); }
        continue;
      }
      if (a.pending) continue;
      if (p.water && s.waterTiles.length && a.thirst > p.drinkHours * 60) { if (planWater(s, a, 'thirsty')) continue; }
      const share = shareAt(s, a, a.tile), rate = forageAt(s, a.tile) * share;
      const L = s.grazed[a.tile];
      if (s.rich[a.tile] > 0) s.grazed[a.tile] = Math.min(1, L + a.mass / 60 * p.grazeRate * (1 - L) * (rate / s.rich[a.tile]));
      a.eaten += rate * a.demand;
      if (rate < a.planned / p.stayFactor) { a.eaten = 0; decide(s, a, 'thinned'); }
      else if (p.mealReplan && a.eaten >= a.amount) { a.eaten = 0; decide(s, a, 'meal eaten'); }
      else if (p.loneHourly && !kinAt(s, a, a.tile) && (s.minute + a.id) % 60 === 0) decide(s, a, 'alone, hourly');
    }
    for (let guard = 0; guard < 50; guard++) {
      let any = false;
      for (const a of s.agents) if (a.pending) {
        const ev = a.pending; a.pending = null; any = true;
        if (a.tile === a.target && a.mode === 'food') reweigh(s, a, ev);
      }
      if (!any) break;
    }
    while (s.arrivalsDue.length && s.arrivalsDue[0] <= s.minute) {
      s.arrivalsDue.shift();
      let t; do { const side = Math.floor(s.R() * 4), k = Math.floor(s.R() * (side < 2 ? s.W : s.H));
        t = side === 0 ? k : side === 1 ? (s.H - 1) * s.W + k : side === 2 ? k * s.W : k * s.W + s.W - 1; } while (s.water[t]);
      const a = newCaribou(s, s.nextId++, t); s.agents.push(a); s.on[t]++; s.load[t] += weight(s, a);
      logEvent(s, { kind: 'arrival', id: a.id, tile: t }); decide(s, a, 'arrived');
    }
    s.carcasses = s.carcasses.filter(c => s.minute - c.minute < 3 * 1440);
    s.minute++;
    if (s.minute % 60 === 0) sample(s);
  }

  function sample(s) {
    const N = Math.max(1, s.agents.length), cl = clusters(s);
    let largest = cl[0] ? cl[0].count : 0, alone = 0, weighted = 0, perTile = 0;
    for (const c of cl) { if (c.count === 1) alone++; weighted += c.count * c.count; }
    const tiles = new Map(); for (const a of s.agents) tiles.set(a.tile, (tiles.get(a.tile) || 0) + 1);
    for (const k of tiles.values()) perTile += k * k;
    const sizes = cl.map(c => c.count), mean = sizes.reduce((a, b) => a + b, 0) / (sizes.length || 1);
    const sd = Math.sqrt(sizes.reduce((a, b) => a + (b - mean) * (b - mean), 0) / (sizes.length || 1));
    let intake = 0;
    for (const a of s.agents) intake += a.eating && a.mode === 'food' && s.rich[a.tile] > 0 ? forageAt(s, a.tile) * shareAt(s, a, a.tile) / s.rich[a.tile] : 0;
    s.stats.hours++;
    s.stats.samples.push({ minute: s.minute, alone: alone / N, largest: largest / N, herds: cl.length, mean: weighted / N,
      perTile: perTile / N, cv: mean ? sd / mean : 0, intake: intake / N, n: s.agents.length });
    if (s.stats.samples.length > 24 * 60) s.stats.samples.shift();
  }

  const groups = s => { const m = new Map(); for (const a of s.agents) { if (!m.has(a.tile)) m.set(a.tile, []); m.get(a.tile).push(a.id); } return m; };
  const api = { DEFAULTS, create, tick, groups, clusters, forecast, capacity, forageAt, byId };
  if (typeof module !== 'undefined') module.exports = api; else root.HerdSim = api;
})(this);
