# BenchmarkDotNet baselines

These benchmarks cover the fusion, cache, and scope hot paths without changing
the public API. BenchmarkDotNet is referenced only by the non-packable
`benchmarks/Observation.Core.Benchmarks` project; the project is not a test
project and is not run by `dotnet test`.

Run the short job from the repository root:

```console
dotnet run --project benchmarks/Observation.Core.Benchmarks -c Release -- --job short
```

The benchmark executable accepts BenchmarkDotNet filters, for example:

```console
dotnet run --project benchmarks/Observation.Core.Benchmarks -c Release -- --job short --filter '*FusionBenchmarks*'
```

## Captured baseline

Captured on **Linux CachyOS, 12th Gen Intel Core i7-12700KF, 20 logical / 12
physical cores**, **.NET SDK 10.0.112 / .NET 10.0.12**, **2026-09-24** with
`--job short` in Release configuration. Values are BenchmarkDotNet means;
the run used 3 warmups and 3 measured iterations.

| Benchmark | Parameters | Mean | Error | StdDev | Allocated |
| --- | --- | ---: | ---: | ---: | ---: |
| Cache hit | — | 312.6 ns | 56.33 ns | 3.09 ns | 824 B |
| Cache miss | — | 1,018.9 ns | 147.46 ns | 8.08 ns | 2,560 B |
| Cache invalidate | — | 10,494.8 ns | 63,477.75 ns | 3,479.43 ns | 2,424 B |
| Fusion resolve | 10, agree | 2.036 μs | 0.5395 μs | 0.0296 μs | 6.13 KB |
| Fusion resolve | 10, conflict | 2.259 μs | 0.1071 μs | 0.0059 μs | 6.73 KB |
| Fusion resolve | 100, agree | 15.129 μs | 2.1105 μs | 0.1157 μs | 23.48 KB |
| Fusion resolve | 100, conflict | 17.806 μs | 1.5707 μs | 0.0861 μs | 24.14 KB |
| Fusion resolve | 1000, agree | 176.422 μs | 10.7594 μs | 0.5898 μs | 188.73 KB |
| Fusion resolve | 1000, conflict | 236.935 μs | 19.1670 μs | 1.0506 μs | 189.46 KB |
| Scope append | depth 4 | 20.986 ns | 12.1600 ns | 0.6665 ns | 112 B |
| Scope compare | depth 4 | 3.669 ns | 0.0777 ns | 0.0043 ns | — |
| Scope append | depth 16 | 26.217 ns | 4.7938 ns | 0.2628 ns | 208 B |
| Scope compare | depth 16 | 9.705 ns | 0.4034 ns | 0.0221 ns | — |

The baseline is informational and is not a CI performance gate. BenchmarkDotNet
reports are also written to `BenchmarkDotNet.Artifacts/results/` locally.
