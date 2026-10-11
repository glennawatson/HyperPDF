# Glyph path grouping on the Radeon

The maintained `PdfViewerLite.GpuProbe` rendered a generated PDF with about 2,900 Helvetica glyphs in 56 lines. The 512 × 512 tile used the same shared Avalonia OpenGL context on an RX 7900 XTX under a private Weston compositor. Each warm result is the mean of 100 operations; GPU replay calls `GRContext.Flush(true, true)` and includes device completion, but not the window swap. Both variants used the 512-entry glyph-path cache. Variant A disabled grouping by comparing the PDF render mode with an unreachable value; B grouped up to four compatible glyphs in one filled vector path. The source and probe were rebuilt for each A/B/B/A pass in one session.

| Runtime and pass | GPU submission | GPU completed replay | CPU replay | First GPU replay | GPU resources | Skia cache |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| .NET 10 A1 | 2.621 ms | 6.603 ms | 3.426 ms | 18.549 ms | 65 | 1,245,299 B |
| .NET 10 B1 | 0.403 ms | 0.630 ms | 3.172 ms | 16.697 ms | 4 | 5,324,800 B |
| .NET 10 B2 | 0.401 ms | 0.642 ms | 3.190 ms | 16.456 ms | 4 | 5,324,800 B |
| .NET 10 A2 | 2.645 ms | 6.626 ms | 3.440 ms | 19.410 ms | 65 | 1,245,299 B |
| .NET 11 A1 | 2.715 ms | 6.805 ms | 3.520 ms | 18.718 ms | 65 | 1,245,299 B |
| .NET 11 B1 | 0.368 ms | 0.594 ms | 3.190 ms | 16.962 ms | 4 | 5,324,800 B |
| .NET 11 B2 | 0.391 ms | 0.618 ms | 3.227 ms | 19.857 ms | 4 | 5,324,800 B |
| .NET 11 A2 | 2.655 ms | 6.687 ms | 3.484 ms | 19.419 ms | 65 | 1,245,299 B |

The warm completed GPU ranges do not overlap. Grouping reduced them by about tenfold on this fixture and device. The CPU replay also fell by about 0.2–0.3 ms with four-glyph groups. A larger 16-glyph group measured 0.59–0.64 ms GPU but raised CPU replay to 4.35–4.42 ms, so the four-glyph limit was retained. The first GPU replay ranges overlap and do not establish a cold-render win. The grouped cache holds about 4.1 MiB more Skia resources in this probe; this is context cache use, not the whole device's memory.

Software pixels from the A and B variants were compared byte by byte. Both frameworks produced the same hashes for each variant. Grouping changed 9,996 of 262,144 pixels, all at antialiased glyph edges, by at most 6 of 255 channel values; 1,501 differed by more than 2 and 44 by more than 4. No solid interior pixel reversed. The three focused square-font tests compare exact output for separate opaque glyphs, overlapping glyphs and paint order. The 16-case GPU standards probe found zero material interior differences and zero independent-oracle failures; its text case had four one-channel-unit GPU-versus-CPU differences. These checks do not make the edge pixels byte-exact.

The local SkiaSharp checkout (`4783f51`) already exposes `SKCanvas.DrawAtlas` through `sk_canvas_draw_atlas`. The local native Skia C wrapper calls `SkCanvas::drawAtlas`, which draws sprites from an `SkImage`; using it for PDF outlines would first require rasterising the outlines and managing an atlas for different zoom levels. The implementation instead uses `SKPathBuilder.AddPath`, which SkiaSharp already binds to native Skia, and keeps paths as vectors. It only groups opaque, solid-colour fill glyphs with separated bounds; other text modes retain their individual draws. Skia's internal path renderer is free to select its own atlas when a grouped path qualifies, but this probe did not identify the renderer it selected.
