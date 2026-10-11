# Map render comparison corpus

These two USGS PDFs provide redistributable map workloads for issue #137. The
1957 Morrison map is a one-page raster map with many JPEG tiles. The 2023 Lost
City US Topo map is a dense, layered page with imagery, contours, labels and
other vector drawing. They exercise large-page scrolling, zooming, image upload
and graphics replay. They do not establish PDF conformance or replace the
independent standards fixtures.

USGS [states that its topographic maps are public domain](https://www.usgs.gov/faqs/are-usgs-topographic-maps-copyrighted),
with three commercial-data exceptions limited to certain US Topo maps. The
selected 1957 historical map and 2023 West Virginia/Virginia map fall outside
those exceptions. The dated download URLs, byte counts and SHA-256 digests are
in [corpus.json](corpus.json). The PDF files are fetched into a local cache,
not checked into the repository.

To fetch and verify the exact files from the repository root:

```bash
dotnet run --file scripts/FetchComparisonCorpus.cs -- tests/render-corpus/corpus.json ~/.cache/pdfviewerlite/render-corpus usgs-morrison-1957 usgs-lost-city-2023
```

Re-running the command checks cached file size and SHA-256 before reuse. The
focused three-engine test independently verifies both cached files and saves
page images and scores outside the repository:

```bash
PVL_REQUIRE_RENDER_CORPUS=1 PVL_REQUIRE_PDFJS=1 dotnet run --project tests/PdfViewerLite.HyperPdf.Tests/PdfViewerLite.HyperPdf.Tests.csproj --framework net11.0 -- --treenode-filter '/*/*/MapCorpusRenderingComparisonTests/*'
```

Set `PDFVIEWERLITE_RENDER_CORPUS_DIR` when using another cache location. The
test records differences without treating either reference renderer as a
standards oracle. Without the required flag, it skips when the maps are absent.

The local WAC 3456 Sydney and WAC 3457 Canberra sheets informed the workload
choice. Each is a roughly 7,016 × 4,961 point page made from JPEG image tiles.
Their copyright does not permit adding them to the remote corpus, and no bytes
from them are included here.
