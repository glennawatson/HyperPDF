```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 18.19 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-JRULTY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/issue-120-bdn-net10/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net10.0  IterationCount=15
WarmupCount=5

```
| Method             | Source    | Mean      | Error     | StdDev    | Ratio | RatioSD |
|------------------- |---------- |----------:|----------:|----------:|------:|--------:|
| **OpenFirstPage**      | **Automatic** |  **4.992 ms** | **0.0415 ms** | **0.0388 ms** |  **1.00** |    **0.00** |
| OpenFirstPageAsync | Automatic |  5.060 ms | 0.0650 ms | 0.0608 ms |  1.01 |    0.01 |
|                    |           |           |           |           |       |         |
| **OpenFirstPage**      | **Memory**    | **10.916 ms** | **0.1308 ms** | **0.1224 ms** |  **1.00** |    **0.00** |
| OpenFirstPageAsync | Memory    | 11.477 ms | 0.1412 ms | 0.1321 ms |  1.05 |    0.02 |
|                    |           |           |           |           |       |         |
| **OpenFirstPage**      | **Mapped**    |  **4.931 ms** | **0.0134 ms** | **0.0112 ms** |  **1.00** |    **0.00** |
| OpenFirstPageAsync | Mapped    |  4.965 ms | 0.0210 ms | 0.0175 ms |  1.01 |    0.00 |
|                    |           |           |           |           |       |         |
| **OpenFirstPage**      | **Stream**    |  **6.729 ms** | **0.0316 ms** | **0.0296 ms** |  **1.00** |    **0.00** |
| OpenFirstPageAsync | Stream    | 11.599 ms | 0.1007 ms | 0.0942 ms |  1.72 |    0.02 |
