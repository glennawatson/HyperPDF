```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 14.7 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-BPGATC : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

Arguments=/p:TargetFramework=net11.0  IterationCount=15  WarmupCount=5

```
| Method            | Workload     | Mean      | Error     | StdDev    |
|------------------ |------------- |----------:|----------:|----------:|
| **ColdOpenAndRender** | **Scan**         | **17.261 ms** | **0.2968 ms** | **0.2777 ms** |
| WarmReplay        | Scan         | 15.802 ms | 0.1547 ms | 0.1292 ms |
| **ColdOpenAndRender** | **Text**         |  **5.455 ms** | **0.0584 ms** | **0.0546 ms** |
| WarmReplay        | Text         |  3.546 ms | 0.1418 ms | 0.1257 ms |
| **ColdOpenAndRender** | **Transparency** |  **6.884 ms** | **0.0434 ms** | **0.0384 ms** |
| WarmReplay        | Transparency |  7.706 ms | 1.1041 ms | 0.9788 ms |
