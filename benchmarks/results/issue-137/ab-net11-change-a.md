```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 16.14 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-BPGATC : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

Arguments=/p:TargetFramework=net11.0  IterationCount=15  WarmupCount=5

```
| Method            | Workload     | Mean      | Error     | StdDev    |
|------------------ |------------- |----------:|----------:|----------:|
| **ColdOpenAndRender** | **Scan**         | **16.520 ms** | **0.1087 ms** | **0.0908 ms** |
| WarmReplay        | Scan         | 15.807 ms | 0.0521 ms | 0.0462 ms |
| **ColdOpenAndRender** | **Text**         |  **5.440 ms** | **0.0507 ms** | **0.0474 ms** |
| WarmReplay        | Text         |  3.462 ms | 0.0244 ms | 0.0216 ms |
| **ColdOpenAndRender** | **Transparency** |  **6.903 ms** | **0.0523 ms** | **0.0489 ms** |
| WarmReplay        | Transparency |  6.788 ms | 0.1022 ms | 0.0853 ms |
