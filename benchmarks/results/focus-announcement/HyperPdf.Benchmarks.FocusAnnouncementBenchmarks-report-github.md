```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor 2.80GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  Job-QVYYKO : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Arguments=/p:TargetFramework=net10.0  

```
| Method               | ScreenReader | Mean     | Error     | StdDev    |
|--------------------- |------------- |---------:|----------:|----------:|
| **FocusChange**          | **False**        | **1.339 ns** | **0.0897 ns** | **0.2604 ns** |
| ClassifyRegistration | False        | 1.132 ns | 0.1350 ns | 0.3917 ns |
| **FocusChange**          | **True**         | **5.841 ns** | **0.2664 ns** | **0.7685 ns** |
| ClassifyRegistration | True         | 1.166 ns | 0.0861 ns | 0.2483 ns |
