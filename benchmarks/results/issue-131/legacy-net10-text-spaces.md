```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 20.77 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-CMWOTZ : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/issue129131-legacy-net10/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net10.0  IterationCount=15
WarmupCount=5

```
| Method         | Spaces | Mean       | Error     | StdDev    |
|--------------- |------- |-----------:|----------:|----------:|
| **RepeatedSpaces** | **0**      |   **2.157 μs** | **0.0114 μs** | **0.0107 μs** |
| BodyText       | 0      |  19.230 μs | 0.1655 μs | 0.1548 μs |
| **RepeatedSpaces** | **32**     |   **7.613 μs** | **0.0195 μs** | **0.0182 μs** |
| BodyText       | 32     |  18.383 μs | 0.1198 μs | 0.1120 μs |
| **RepeatedSpaces** | **256**    |  **63.135 μs** | **0.2460 μs** | **0.2301 μs** |
| BodyText       | 256    |  18.384 μs | 0.1175 μs | 0.1099 μs |
| **RepeatedSpaces** | **1024**   | **635.467 μs** | **0.3853 μs** | **0.3217 μs** |
| BodyText       | 1024   |  18.221 μs | 0.1058 μs | 0.0938 μs |
