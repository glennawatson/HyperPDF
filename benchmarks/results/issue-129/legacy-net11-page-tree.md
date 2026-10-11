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
| Method | Mean        | Error    | StdDev   | Ratio | RatioSD |
|------- |------------:|---------:|---------:|------:|--------:|
| Tiny   |    684.5 ns |  3.08 ns |  2.73 ns |  1.00 |    0.00 |
| Wide   | 10,510.5 ns | 57.27 ns | 53.57 ns | 15.35 |    0.10 |
| Deep   |  3,078.0 ns | 12.85 ns | 12.02 ns |  4.50 |    0.02 |
