```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 18.32 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-MDTLYB : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/tmp/pdf137-baseline-net10/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net10.0  IterationCount=15
WarmupCount=5

```
| Method                    | Workload     | Mean      | Error     | StdDev    |
|-------------------------- |------------- |----------:|----------:|----------:|
| **ColdOpenAndRender**         | **Scan**         | **17.260 ms** | **0.2294 ms** | **0.2033 ms** |
| FirstRecordOnOpenDocument | Scan         | 16.081 ms | 0.1102 ms | 0.0977 ms |
| WarmReplay                | Scan         | 15.812 ms | 0.0930 ms | 0.0726 ms |
| WarmZoomTile              | Scan         |  8.422 ms | 0.0750 ms | 0.0701 ms |
| WarmScrollTile            | Scan         | 15.153 ms | 0.5719 ms | 0.4775 ms |
| **ColdOpenAndRender**         | **Text**         |  **9.868 ms** | **3.3277 ms** | **3.1127 ms** |
| FirstRecordOnOpenDocument | Text         |  5.944 ms | 0.2243 ms | 0.2098 ms |
| WarmReplay                | Text         |  3.561 ms | 0.0737 ms | 0.0690 ms |
| WarmZoomTile              | Text         |  3.726 ms | 0.5322 ms | 0.4444 ms |
| WarmScrollTile            | Text         |  2.579 ms | 0.4799 ms | 0.4254 ms |
| **ColdOpenAndRender**         | **Transparency** |  **7.194 ms** | **0.1343 ms** | **0.1048 ms** |
| FirstRecordOnOpenDocument | Transparency |  9.062 ms | 2.5006 ms | 2.3390 ms |
| WarmReplay                | Transparency |  6.858 ms | 0.0446 ms | 0.0418 ms |
| WarmZoomTile              | Transparency |  8.356 ms | 0.1194 ms | 0.1058 ms |
| WarmScrollTile            | Transparency |  5.119 ms | 0.0396 ms | 0.0370 ms |
