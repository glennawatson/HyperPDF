```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 20.47 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-VIOEZP : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/issue129131-current-net10/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net10.0  IterationCount=15
WarmupCount=5

```
| Method         | Spaces | Mean       | Error     | StdDev    |
|--------------- |------- |-----------:|----------:|----------:|
| **RepeatedSpaces** | **0**      |   **2.185 μs** | **0.0104 μs** | **0.0097 μs** |
| BodyText       | 0      |  18.471 μs | 0.1130 μs | 0.1002 μs |
| **RepeatedSpaces** | **32**     |   **6.863 μs** | **0.0122 μs** | **0.0102 μs** |
| BodyText       | 32     |  18.466 μs | 0.1512 μs | 0.1415 μs |
| **RepeatedSpaces** | **256**    |  **39.889 μs** | **0.1154 μs** | **0.1023 μs** |
| BodyText       | 256    |  18.411 μs | 0.0940 μs | 0.0879 μs |
| **RepeatedSpaces** | **1024**   | **181.434 μs** | **0.2948 μs** | **0.2462 μs** |
| BodyText       | 1024   |  18.393 μs | 0.1004 μs | 0.0890 μs |
