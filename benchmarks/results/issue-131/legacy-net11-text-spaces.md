```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 21.22 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-DLVVCO : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/issue129131-legacy-net11/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net11.0  IterationCount=15
WarmupCount=5

```
| Method         | Spaces | Mean       | Error     | StdDev    |
|--------------- |------- |-----------:|----------:|----------:|
| **RepeatedSpaces** | **0**      |   **2.052 μs** | **0.0117 μs** | **0.0103 μs** |
| BodyText       | 0      |  17.339 μs | 0.0447 μs | 0.0373 μs |
| **RepeatedSpaces** | **32**     |   **7.614 μs** | **0.0359 μs** | **0.0318 μs** |
| BodyText       | 32     |  17.791 μs | 0.0938 μs | 0.0832 μs |
| **RepeatedSpaces** | **256**    |  **63.342 μs** | **0.2574 μs** | **0.2010 μs** |
| BodyText       | 256    |  17.301 μs | 0.0743 μs | 0.0695 μs |
| **RepeatedSpaces** | **1024**   | **595.276 μs** | **2.0622 μs** | **1.8281 μs** |
| BodyText       | 1024   |  17.607 μs | 0.1030 μs | 0.0860 μs |
