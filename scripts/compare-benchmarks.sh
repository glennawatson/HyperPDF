#!/usr/bin/env bash
# Measures the benchmarks matching a filter on a baseline commit and on the working tree, both with the working tree's
# benchmark code, then compares them from EventPipe traces: allocations per operation with no leeway, since they are
# deterministic, and CPU time per operation and per method with a leeway for shared machines (default 5%).
# Usage: scripts/compare-benchmarks.sh <baseline ref> '<filter> [more filters]' [leeway percent]
# Runs on net11.0, the runtime the app ships with; set BENCHMARK_FRAMEWORK=net10.0 to compare the other target.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REF="${1:?baseline ref}"
read -r -a FILTERS <<< "${2:?benchmark filter}"
LEEWAY="${3:-5}"
FRAMEWORK="${BENCHMARK_FRAMEWORK:-net11.0}"
OUT="$ROOT/benchmarks/HyperPdf.Benchmarks/BenchmarkDotNet.Comparison"
BASE="$(mktemp -d)/baseline"
rm -rf "$OUT"
mkdir -p "$OUT"
git -C "$ROOT" worktree add --detach "$BASE" "$REF" >/dev/null
trap 'git -C "$ROOT" worktree remove --force "$BASE"' EXIT
tar -C "$ROOT/benchmarks/HyperPdf.Benchmarks" --exclude=bin --exclude=obj --exclude='BenchmarkDotNet.*' -cf - . \
  | tar -C "$BASE/benchmarks/HyperPdf.Benchmarks" -xf -

# BenchmarkDotNet builds the benchmark project it finds under the working directory, so each run starts in its own tree.
run() {
  (cd "$1" && dotnet run -c Release -f "$FRAMEWORK" --project "$1/benchmarks/HyperPdf.Benchmarks" -- --filter "${FILTERS[@]}" --artifacts "$2" "${@:3}")
}

# Allocations are deterministic, so one launch with one warmup and one long iteration is enough: a long iteration measures
# many operations, so the runtime's batched samples blur the totals very little. The CPU runs below keep their repeats.
ALLOC_JOB=(--launchCount 1 --iterationTime 1000 --iterationCount 1 --warmupCount 1)
run "$BASE" "$OUT/baseline-alloc" "${ALLOC_JOB[@]}"
run "$ROOT" "$OUT/change-alloc" "${ALLOC_JOB[@]}"
# CPU: baseline, change, change, baseline, so a machine that speeds up or slows down over the runs evens out.
run "$BASE" "$OUT/baseline-cpu-1" --profiler EP
run "$ROOT" "$OUT/change-cpu-1" --profiler EP
run "$ROOT" "$OUT/change-cpu-2" --profiler EP
run "$BASE" "$OUT/baseline-cpu-2" --profiler EP

status=0
dotnet run --file "$ROOT/scripts/AllocationAudit.cs" -- "$OUT/change-alloc" "$ROOT/benchmarks/allocations-explained.json" --baseline "$OUT/baseline-alloc" || status=1
dotnet run --file "$ROOT/scripts/CpuAudit.cs" -- "$OUT/change-cpu-1,$OUT/change-cpu-2" --baseline "$OUT/baseline-cpu-1,$OUT/baseline-cpu-2" --leeway "$LEEWAY" || status=1
exit $status
