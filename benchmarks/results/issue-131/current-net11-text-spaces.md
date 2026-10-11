```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 20.93 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-XQECXW : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/issue129131-current-net11/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net11.0  IterationCount=15
WarmupCount=5

```
| Method         | Spaces | Mean       | Error     | StdDev    |
|--------------- |------- |-----------:|----------:|----------:|
| **RepeatedSpaces** | **0**      |   **2.079 μs** | **0.0146 μs** | **0.0137 μs** |
| BodyText       | 0      |  17.451 μs | 0.1289 μs | 0.1206 μs |
| **RepeatedSpaces** | **32**     |   **7.062 μs** | **0.0268 μs** | **0.0237 μs** |
| BodyText       | 32     |  17.591 μs | 0.0526 μs | 0.0466 μs |
| **RepeatedSpaces** | **256**    |  **40.745 μs** | **0.1551 μs** | **0.1450 μs** |
| BodyText       | 256    |  17.482 μs | 0.0701 μs | 0.0656 μs |
| **RepeatedSpaces** | **1024**   | **142.133 μs** | **0.3964 μs** | **0.3310 μs** |
| BodyText       | 1024   |  17.823 μs | 0.0715 μs | 0.0597 μs |
