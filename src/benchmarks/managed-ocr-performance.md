# Managed text recognition

`ManagedOcrBenchmarks` times the stages of `ManagedOcrEngine` on a page of thirty lines at 300 DPI (2550 by 3300 pixels),
with the `tessdata_fast` English model. BenchmarkDotNet short job, .NET 11 RC2, x64 with AVX2 and FMA, Linux. Each row
compares the first working version with the version after the changes below; the same machine, build and benchmark
class were used for both.

| Stage | First version | Now | Change |
|---|---|---|---|
| Separate ink from paper (`BuildInkMask`) | 10.29 ms | 3.02 ms | 3.4 times faster |
| Find the blobs of ink (`LabelBlobs`) | 3.05 ms | 0.91 ms | 3.4 times faster |
| Read one line (`ReadLine`) | 15.19 ms | 6.25 ms | 2.4 times faster |
| Read the whole page (`RecognizePage`) | 489.06 ms | 185.45 ms | 2.6 times faster |

What changed:

* Weights are stored a row per input, so one pass over a row of weights serves four steps at once and the outputs fill
  whole vectors (`MatrixKernels`). The gates of a recurrent layer are joined into one matrix.
* The part of every gate that depends only on the layer's input is computed for all steps at once; only the part that
  depends on the previous output waits for the step before. The rows of the summary layer run together.
* The squashing functions work a vector at a time with a polynomial exponential (error about 1e-6 against the exact
  function, tested over a dense sweep).
* Ink and paper are separated with vector compares and a four way histogram; runs of ink are found with vectorised
  searches, so blank rows cost almost nothing.
* All grids, arrays, lists and the mask come from the recogniser's `RecognitionScratch`. After warm-up, reading a page
  allocates only the text of each recognised word (`AuditAllocations`: 0 B/op for the first three stages).

Limits: one page, one machine, short job. The 15 MB `tessdata_best` English model has a 512 cell layer whose weights do
not fit the cache, so it is memory bound; in a quick check with a probe program (not BenchmarkDotNet) it read lines about
three times slower than the fast model. Weights are held as
32 bit floats; integer models are widened on load, so the integer code paths of the original engine are not used.
