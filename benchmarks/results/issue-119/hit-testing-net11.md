```

BenchmarkDotNet v0.16.0-preview.2, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 62.69 GB Total, 21.58 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-KRZWII : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

EnvironmentVariables=DOTNET_EnableEventPipe=1,DOTNET_EventPipeOutputPath=/home/glennwatson/.cache/pdfviewerlite/issue-137-evidence/hit-adapter-net11/alloc-{pid}.nettrace,DOTNET_EventPipeConfig=Microsoft-Windows-DotNETRuntime:0x41280001:5,BenchmarkDotNet.EngineEventSource:0xFFFFFFFFFFFFFFFF:5  Arguments=/p:TargetFramework=net11.0  IterationCount=15
WarmupCount=5

```
| Method                 | Mean       | Error    | StdDev   |
|----------------------- |-----------:|---------:|---------:|
| PdfiumHitTest          | 9,924.4 ns | 60.89 ns | 56.96 ns |
| HyperPdfHitTest        |   540.6 ns |  5.95 ns |  5.56 ns |
| HyperPdfAdapterHitTest |   593.3 ns |  3.78 ns |  3.53 ns |
