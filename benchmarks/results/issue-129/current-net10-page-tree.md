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
| Method | Mean        | Error    | StdDev   | Ratio | RatioSD |
|------- |------------:|---------:|---------:|------:|--------:|
| Tiny   |    700.3 ns |  2.71 ns |  2.26 ns |  1.00 |    0.00 |
| Wide   | 10,920.4 ns | 32.31 ns | 28.64 ns | 15.59 |    0.06 |
| Deep   |  2,837.4 ns | 10.10 ns |  9.45 ns |  4.05 |    0.02 |
