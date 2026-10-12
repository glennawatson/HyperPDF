```

BenchmarkDotNet v0.15.8, Linux Fedora Linux 45 (KDE Plasma Desktop Edition Prerelease)
AMD Ryzen 7 5800X 1.75GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-SXNHPH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Arguments=/p:TargetFramework=net10.0  IterationCount=15  WarmupCount=5

```
| Method            | Mean       | Error   | StdDev  | Ratio | RatioSD |
|------------------ |-----------:|--------:|--------:|------:|--------:|
| PathFill          |   686.3 ns | 4.94 ns | 4.13 ns |  1.00 |    0.01 |
| SolidRectangle    |   353.1 ns | 2.50 ns | 2.09 ns |  0.51 |    0.00 |
| RectangleGeometry |   366.4 ns | 1.09 ns | 0.85 ns |  0.53 |    0.00 |
| RoundedRectangle  | 2,024.9 ns | 7.66 ns | 6.79 ns |  2.95 |    0.02 |
