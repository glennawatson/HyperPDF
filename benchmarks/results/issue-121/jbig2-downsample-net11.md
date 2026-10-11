```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 23.4 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-LBQYNA : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/jbig2-net11/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net11.0  IterationCount=15
WarmupCount=5

```
| Method   | Fixture | Levels | Mean        | Error    | StdDev   | Ratio |
|--------- |-------- |------- |------------:|---------:|---------:|------:|
| **Scalar**   | **Archive** | **1**      |  **7,007.0 μs** | **32.46 μs** | **28.77 μs** |  **1.00** |
| BitCount | Archive | 1      |  4,004.0 μs | 20.94 μs | 18.56 μs |  0.57 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Archive** | **2**      |  **4,966.6 μs** | **29.82 μs** | **27.90 μs** |  **1.00** |
| BitCount | Archive | 2      |  1,322.2 μs |  4.28 μs |  3.80 μs |  0.27 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Archive** | **3**      |  **4,150.7 μs** | **17.71 μs** | **16.56 μs** |  **1.00** |
| BitCount | Archive | 3      |    524.7 μs |  1.01 μs |  0.94 μs |  0.13 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Archive** | **4**      |  **3,941.7 μs** | **15.96 μs** | **14.93 μs** |  **1.00** |
| BitCount | Archive | 4      |    299.5 μs |  0.92 μs |  0.82 μs |  0.08 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Google**  | **1**      | **21,776.4 μs** | **80.32 μs** | **71.20 μs** |  **1.00** |
| BitCount | Google  | 1      | 12,119.3 μs | 34.20 μs | 31.99 μs |  0.56 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Google**  | **2**      | **15,368.1 μs** | **34.97 μs** | **31.00 μs** |  **1.00** |
| BitCount | Google  | 2      |  4,127.2 μs | 22.03 μs | 20.61 μs |  0.27 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Google**  | **3**      | **12,969.4 μs** | **36.52 μs** | **34.16 μs** |  **1.00** |
| BitCount | Google  | 3      |  1,600.2 μs |  6.86 μs |  6.08 μs |  0.12 |
|          |         |        |             |          |          |       |
| **Scalar**   | **Google**  | **4**      | **12,482.8 μs** | **36.37 μs** | **30.37 μs** |  **1.00** |
| BitCount | Google  | 4      |    943.7 μs |  2.14 μs |  2.00 μs |  0.08 |
