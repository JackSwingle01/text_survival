// The game runs here, off the page's thread: the .NET runtime, Sim and the host. The page talks to it
// only through client.js, by message: { id, op, arg } in, { id, ok, value | error } out. Views go back
// as parsed objects; saves as bytes.
import { dotnet } from './_framework/dotnet.js';

const ready = (async () => {
  const { getAssemblyExports, getConfig } = await dotnet.create();
  const exports = await getAssemblyExports(getConfig().mainAssemblyName);
  return exports.Game;
})();

const ops = {
  newGame: (G, seed) => JSON.parse(G.NewGame(seed)),
  act: (G, action) => JSON.parse(G.Act(JSON.stringify(action))),
  view: G => JSON.parse(G.View()),
  save: G => G.Save(),
  load: (G, bytes) => JSON.parse(G.Load(bytes)),
  scenario: (G, name) => JSON.parse(G.Scenario(name)),
  // Development only: Observer mode (observe.html).
  observeNew: (G, seed) => JSON.parse(G.ObserveNew(seed)),
  observeView: G => JSON.parse(G.ObserveView()),
  observeAdvance: (G, minutes) => JSON.parse(G.ObserveAdvance(minutes)),
  observeSpawn: (G, { kind, x, y }) => JSON.parse(G.ObserveSpawn(kind, x, y)),
  observeInspect: (G, { x, y }) => JSON.parse(G.ObserveInspect(x, y)),
};

self.onmessage = async ({ data: { id, op, arg } }) => {
  try {
    const G = await ready;
    if (!ops[op]) throw new Error(`No operation '${op}'`);
    const t = performance.now();
    const value = ops[op](G, arg);
    self.postMessage({ id, ok: true, value, ms: performance.now() - t });
  } catch (e) {
    self.postMessage({ id, ok: false, error: String(e?.message ?? e) });
  }
};
