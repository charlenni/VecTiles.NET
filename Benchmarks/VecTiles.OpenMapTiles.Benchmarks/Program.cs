using BenchmarkDotNet.Running;
using VecTiles.OpenMapTiles.Benchmarks;

// The suite selection is done with "--suite <name>"; both arguments are removed
// before the remaining arguments are passed to BenchmarkDotNet.
int suiteIndex = Array.FindIndex(args, arg => arg == "--suite");
string suite = suiteIndex >= 0 && suiteIndex + 1 < args.Length ? args[suiteIndex + 1] : "render";
var benchmarkArgs = args.Where((_, index) => index != suiteIndex && index != suiteIndex + 1).ToArray();

switch (suite)
{
    case "expressions":
        BenchmarkRunner.Run<OMTExpressionBenchmarks>(args: benchmarkArgs);
        break;
    case "converter":
        BenchmarkRunner.Run<OMTConverterBenchmarks>(args: benchmarkArgs);
        break;
    default:
        BenchmarkRunner.Run<OMTRenderBenchmarks>(args: benchmarkArgs);
        break;
}
