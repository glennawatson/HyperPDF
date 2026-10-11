```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 23.22 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-TVCKJN : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/jbig2-net10/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net10.0  IterationCount=15
WarmupCount=5

```
| Method   | Fixture | Levels | Mean        | Error    | StdDev   | Ratio |
|--------- |-------- |------- |------------:|---------:|---------:|------:|
| **Scalar**   | **Archive** | **1**      |  **7,253.8 μs** | **29.16 μs** | **25.85 μs** |  **1.00** |
| BitCount | Archive | 1      |  3,904.6 μs | 16.53 μs | 15.46 μs |  0.54 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Archive** | **2**      |  **4,936.7 μs** | **24.18 μs** | **21.43 μs** |  **1.00** |
| BitCount | Archive | 2      |  1,335.1 μs |  6.84 μs |  6.40 μs |  0.27 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Archive** | **3**      |  **4,140.8 μs** | **13.68 μs** | **12.80 μs** |  **1.00** |
| BitCount | Archive | 3      |    522.7 μs |  1.64 μs |  1.54 μs |  0.13 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Archive** | **4**      |  **3,934.7 μs** | **13.34 μs** | **11.83 μs** |  **1.00** |
| BitCount | Archive | 4      |    313.5 μs |  0.91 μs |  0.85 μs |  0.08 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Google**  | **1**      | **22,531.7 μs** | **72.08 μs** | **67.43 μs** |  **1.00** |
| BitCount | Google  | 1      | 12,538.1 μs | 79.17 μs | 74.06 μs |  0.56 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Google**  | **2**      | **15,598.5 μs** | **62.82 μs** | **58.76 μs** |  **1.00** |
| BitCount | Google  | 2      |  4,103.5 μs |  8.21 μs |  7.27 μs |  0.26 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Google**  | **3**      | **13,005.2 μs** | **26.80 μs** | **22.38 μs** |  **1.00** |
| BitCount | Google  | 3      |  1,603.9 μs |  4.90 μs |  4.59 μs |  0.12 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Google**  | **4**      | **12,506.8 μs** | **37.87 μs** | **35.42 μs** |  **1.00** |
| BitCount | Google  | 4      |    956.3 μs |  3.89 μs |  3.64 μs |  0.08 |
