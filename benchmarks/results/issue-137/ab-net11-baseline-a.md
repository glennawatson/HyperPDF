```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 12.69 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-BPGATC : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

Arguments=/p:TargetFramework=net11.0  IterationCount=15  WarmupCount=5

```
| Method            | Workload     | Mean      | Error     | StdDev    |
|------------------ |------------- |----------:|----------:|----------:|
| **ColdOpenAndRender** | **Scan**         | **16.687 ms** | **0.2724 ms** | **0.2415 ms** |
| WarmReplay        | Scan         | 15.931 ms | 0.2754 ms | 0.2300 ms |
| **ColdOpenAndRender** | **Text**         |  **5.589 ms** | **0.1230 ms** | **0.1151 ms** |
| WarmReplay        | Text         |  3.761 ms | 0.3902 ms | 0.3258 ms |
| **ColdOpenAndRender** | **Transparency** |  **7.039 ms** | **0.0879 ms** | **0.0734 ms** |
| WarmReplay        | Transparency |  6.843 ms | 0.2131 ms | 0.1889 ms |
