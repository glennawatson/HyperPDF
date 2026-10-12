# HyperPdf.Skia

The app's renderer uses SkiaSharp 4.153.1 under its own assembly name. App builder extensions live in HyperPdf.App. HarfBuzzSharp supplies OpenType shaping with cached fonts and a reusable buffer per thread.

The renderer compiles against Avalonia's implementation assemblies because its reference assemblies prevent third-party implementations of platform contracts. It does not modify Avalonia assemblies or use reflection or private accessors. NuGet pruning removes the unused Avalonia.Skia dependency declared by Avalonia.X11; the X11 DLL has no assembly reference to it.

Small gradients call the native Skia gradient factories with spans. The paint retains the shader, and the temporary native reference is released. These entry points are tied to the pinned SkiaSharp version. Native AOT resolves the library beside the executable using an absolute path.

This backend covers the viewer's drawing needs rather than every Avalonia feature. Direct bitmap drawing, geometry, text, clipping and scene brushes are supported. Image brushes are unused by the app and unsupported. GPU support uses OpenGL; macOS selects OpenGL or software rather than Metal.

See [performance measurements](../../benchmarks/skia4-performance.md) for timings, allocations and verification limits.
