#!/usr/bin/env bash
# Runs the benchmarks with allocation tracing and fails when HyperPDF allocates anything not listed in
# benchmarks/allocations-explained.json. Pass a BenchmarkDotNet filter, for example '*TileRender*'.
# Runs on net11.0, the runtime the app ships with; set BENCHMARK_FRAMEWORK=net10.0 to audit the other target.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FILTER="${1:-*}"
ARTIFACTS="$ROOT/benchmarks/HyperPdf.Benchmarks/BenchmarkDotNet.Artifacts"
rm -rf "$ARTIFACTS"
dotnet run -c Release -f "${BENCHMARK_FRAMEWORK:-net11.0}" --project "$ROOT/benchmarks/HyperPdf.Benchmarks" -- --filter "$FILTER" --job short --artifacts "$ARTIFACTS"
dotnet run --file "$ROOT/scripts/AllocationAudit.cs" -- "$ARTIFACTS" "$ROOT/benchmarks/allocations-explained.json"
