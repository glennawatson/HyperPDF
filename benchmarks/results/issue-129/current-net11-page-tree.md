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
| Method | Mean        | Error    | StdDev   | Ratio | RatioSD |
|------- |------------:|---------:|---------:|------:|--------:|
| Tiny   |    593.9 ns |  2.69 ns |  2.25 ns |  1.00 |    0.00 |
| Wide   | 10,762.0 ns | 63.03 ns | 58.96 ns | 18.12 |    0.12 |
| Deep   |  2,889.7 ns | 12.39 ns | 10.98 ns |  4.87 |    0.03 |
