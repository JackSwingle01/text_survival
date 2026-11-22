// Native timing for the spike: the same Bench steps the wasm page runs.
int seed = args.Length > 0 ? int.Parse(args[0]) : 1;
int days = args.Length > 1 ? int.Parse(args[1]) : 30;
// WARM=1: run a short bench on another seed first, so the timed run sees tiered-up JIT code.
if (Environment.GetEnvironmentVariable("WARM") == "1") WebSpike.Bench.Run(seed + 1, 3);
var b = WebSpike.Bench.Run(seed, days, Console.Error.WriteLine);
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(b.Result, WebSpike.HostJson.Default.BenchResult));
if (args.Length > 2) File.WriteAllText(args[2], b.WorldJson()); // the world after the bench, to diff against wasm
