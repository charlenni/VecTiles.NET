using BenchmarkDotNet.Running;
using VecTiles.MapLibre.Benchmarks;

// The suite selection is done with "--suite <name>"; both arguments are removed
// before the remaining arguments are passed to BenchmarkDotNet.
int suiteIndex = Array.FindIndex(args, arg => arg == "--suite");
string suite = suiteIndex >= 0 && suiteIndex + 1 < args.Length ? args[suiteIndex + 1] : "render";
var benchmarkArgs = args.Where((_, index) => index != suiteIndex && index != suiteIndex + 1).ToArray();

switch (suite)
{
    case "expressions":
        BenchmarkRunner.Run<ExpressionBenchmarks>(args: benchmarkArgs);
        break;
    case "drawtile":
        BenchmarkRunner.Run<DrawTileBenchmarks>(args: benchmarkArgs);
        break;
    case "converter":
        BenchmarkRunner.Run<ConverterBenchmarks>(args: benchmarkArgs);
        break;
    default:
        BenchmarkRunner.Run<RenderBenchmarks>(args: benchmarkArgs);
        break;
}
