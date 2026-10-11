```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 12.91 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-BPGATC : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

Arguments=/p:TargetFramework=net11.0  IterationCount=15  WarmupCount=5

```
| Method            | Workload     | Mean      | Error     | StdDev    |
|------------------ |------------- |----------:|----------:|----------:|
| **ColdOpenAndRender** | **Scan**         | **16.919 ms** | **0.6295 ms** | **0.5256 ms** |
| WarmReplay        | Scan         | 16.146 ms | 0.7985 ms | 0.6234 ms |
| **ColdOpenAndRender** | **Text**         |  **5.552 ms** | **0.1276 ms** | **0.1194 ms** |
| WarmReplay        | Text         |  3.577 ms | 0.1222 ms | 0.1143 ms |
| **ColdOpenAndRender** | **Transparency** |  **7.003 ms** | **0.1293 ms** | **0.1210 ms** |
| WarmReplay        | Transparency |  6.891 ms | 0.0613 ms | 0.0544 ms |
