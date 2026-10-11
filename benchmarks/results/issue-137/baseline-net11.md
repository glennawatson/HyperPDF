```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 20.97 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-GVAWYJ : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/tmp/pdf137-baseline-net11/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net11.0  IterationCount=15
WarmupCount=5

```
| Method                    | Workload     | Mean      | Error     | StdDev    |
|-------------------------- |------------- |----------:|----------:|----------:|
| **ColdOpenAndRender**         | **Scan**         | **16.975 ms** | **0.1978 ms** | **0.1850 ms** |
| FirstRecordOnOpenDocument | Scan         | 16.024 ms | 0.1951 ms | 0.1825 ms |
| WarmReplay                | Scan         | 15.848 ms | 0.1178 ms | 0.0984 ms |
| WarmZoomTile              | Scan         |  8.758 ms | 0.3287 ms | 0.2914 ms |
| WarmScrollTile            | Scan         | 16.708 ms | 2.0067 ms | 1.7789 ms |
| **ColdOpenAndRender**         | **Text**         |  **5.873 ms** | **0.0841 ms** | **0.0746 ms** |
| FirstRecordOnOpenDocument | Text         |  5.766 ms | 0.2519 ms | 0.2233 ms |
| WarmReplay                | Text         |  3.576 ms | 0.0517 ms | 0.0432 ms |
| WarmZoomTile              | Text         |  3.300 ms | 0.4325 ms | 0.3612 ms |
| WarmScrollTile            | Text         |  2.194 ms | 0.0430 ms | 0.0402 ms |
| **ColdOpenAndRender**         | **Transparency** |  **7.820 ms** | **0.9534 ms** | **0.7962 ms** |
| FirstRecordOnOpenDocument | Transparency |  6.977 ms | 0.1016 ms | 0.0900 ms |
| WarmReplay                | Transparency |  7.168 ms | 0.5127 ms | 0.4545 ms |
| WarmZoomTile              | Transparency |  8.513 ms | 0.3066 ms | 0.2560 ms |
| WarmScrollTile            | Transparency |  5.108 ms | 0.0430 ms | 0.0403 ms |
