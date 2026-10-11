```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 18.4 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-PAINNL : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/issue-120-bdn-net11-corrected/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net11.0  IterationCount=15
WarmupCount=5

```
| Method             | Source    | Mean      | Error     | StdDev    | Ratio | RatioSD |
|------------------- |---------- |----------:|----------:|----------:|------:|--------:|
| **OpenFirstPage**      | **Automatic** |  **4.733 ms** | **0.0200 ms** | **0.0177 ms** |  **1.00** |    **0.00** |
| OpenFirstPageAsync | Automatic |  4.695 ms | 0.0210 ms | 0.0186 ms |  0.99 |    0.01 |
|                    |           |           |           |           |       |         |
| **OpenFirstPage**      | **Memory**    | **10.667 ms** | **0.1697 ms** | **0.1588 ms** |  **1.00** |    **0.00** |
| OpenFirstPageAsync | Memory    | 10.548 ms | 0.0967 ms | 0.0857 ms |  0.99 |    0.02 |
|                    |           |           |           |           |       |         |
| **OpenFirstPage**      | **Mapped**    |  **4.712 ms** | **0.0319 ms** | **0.0298 ms** |  **1.00** |    **0.00** |
| OpenFirstPageAsync | Mapped    |  4.686 ms | 0.0224 ms | 0.0199 ms |  0.99 |    0.01 |
|                    |           |           |           |           |       |         |
| **OpenFirstPage**      | **Stream**    |  **6.623 ms** | **0.0242 ms** | **0.0227 ms** |  **1.00** |    **0.00** |
| OpenFirstPageAsync | Stream    | 11.153 ms | 0.1053 ms | 0.0985 ms |  1.68 |    0.02 |
