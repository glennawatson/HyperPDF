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
| Method | Mean        | Error    | StdDev   | Ratio | RatioSD |
|------- |------------:|---------:|---------:|------:|--------:|
| Tiny   |    776.6 ns |  4.07 ns |  3.81 ns |  1.00 |    0.00 |
| Wide   | 12,362.2 ns | 43.55 ns | 38.60 ns | 15.92 |    0.09 |
| Deep   |  3,349.3 ns | 11.24 ns |  9.96 ns |  4.31 |    0.02 |
