```

BenchmarkDotNet v0.15.8, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-SXNHPH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Arguments=/p:TargetFramework=net10.0  IterationCount=15  WarmupCount=5

```
| Method             | Mean     | Error     | StdDev    |
|------------------- |---------:|----------:|----------:|
| ShapeText          | 3.544 μs | 0.3614 μs | 0.3204 μs |
| DrawCachedGlyphRun | 3.291 μs | 0.0266 μs | 0.0222 μs |
