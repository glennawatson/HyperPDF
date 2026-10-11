// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using Avalonia.Vulkan;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Benchmarks;
using PdfViewerLite.Skia;
using SkiaSharp;

namespace PdfViewerLite.GpuProbe;

/// <summary>Measures completed GPU PDF rendering on Avalonia's presentation context.</summary>
public static class Program
{
    /// <summary>The argument count when a tile offset is supplied.</summary>
    private const int OffsetArgumentCount = 3;

    /// <summary>The exit code for invalid arguments.</summary>
    private const int UsageExitCode = 2;

    /// <summary>The exit code for a failed probe.</summary>
    private const int FailedExitCode = 1;

    /// <summary>The Vulkan minor version needed by the current Skia backend.</summary>
    private const int VulkanMinorVersion = 4;

    /// <summary>An intentionally unavailable Vulkan extension used to check fallback ordering.</summary>
    private const string UnsupportedVulkanExtension = "VK_PVL_fallback_probe_unavailable";

    /// <summary>Enables one-time GPU tone pixel readback and completed tone-pass timing.</summary>
    private const string ToneParityOption = "--tone-parity";

    /// <summary>Enables one-time GPU readback across generated standards fixtures.</summary>
    private const string StandardsParityOption = "--standards-parity";

    /// <summary>The environment variable used to measure a zoomed tile.</summary>
    private const string ScaleVariable = "PDFVIEWERLITE_GPU_PROBE_SCALE";

    /// <summary>Selects the X11 compositor for the private XWayland smoke probe.</summary>
    private const string X11Variable = "PDFVIEWERLITE_GPU_PROBE_X11";

    /// <summary>Writes benchmark errors.</summary>
    private static readonly TextWriter Error = Console.Error;

    /// <summary>Opens a PDF, records its first page and starts the compositor probe.</summary>
    /// <param name="args">The PDF path and optional tile offsets.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        var toneParity = args.Length > 0 && args[^1] == ToneParityOption;
        var standardsParity = args.Length > 0 && args[^1] == StandardsParityOption;
        var sourceArguments = CountSourceArguments(args);
        if (sourceArguments is not (1 or OffsetArgumentCount) || !OperatingSystem.IsLinux())
        {
            await Console.Error.WriteLineAsync("Usage: GpuProbe <PDF path> [offset-x offset-y] [--tone-parity|--standards-parity]");
            return UsageExitCode;
        }

        var path = args[0];
        var offsetX = sourceArguments == OffsetArgumentCount ? int.Parse(args[1], CultureInfo.InvariantCulture) : 0;
        var offsetY = sourceArguments == OffsetArgumentCount ? int.Parse(args[2], CultureInfo.InvariantCulture) : 0;
        if (!TryGetScale(out var scale))
        {
            await Error.WriteLineAsync("The GPU probe scale must be a finite positive number.");
            return UsageExitCode;
        }

        PdfDrawingServices.Register(new PdfRenderBackendRegistration().UseSkia());

        var source = await ReadSourceAsync(path);
        var openStart = Stopwatch.GetTimestamp();
        using var document = PdfDocumentReader.Open(source, null);
        using var renderer = new PdfPageRenderer(document);
        var request = new PdfTileRequest(0, scale, 0, offsetX, offsetY, PdfRenderFlags.None);
        var openTime = Stopwatch.GetElapsedTime(openStart);
        var recordStart = Stopwatch.GetTimestamp();
        if (!renderer.Prepare(request, CancellationToken.None))
        {
            await Console.Error.WriteLineAsync("Page zero could not be recorded.");
            return FailedExitCode;
        }

        var recordTime = Stopwatch.GetElapsedTime(recordStart);
        var state = new GpuProbeState(renderer, request, openTime, recordTime, toneParity, standardsParity);
        GpuProbeApp.State = state;
        try
        {
            _ = CreateAppBuilder().StartWithClassicDesktopLifetime([]);
            await SaveCpuDumpAsync(state).ConfigureAwait(false);
            return state.ExitCode;
        }
        finally
        {
            GpuProbeApp.State = null;
        }
    }

    /// <summary>Writes optional diagnostic pixels after the compositor has stopped.</summary>
    /// <param name="state">The completed probe.</param>
    /// <returns>A task.</returns>
    private static async Task SaveCpuDumpAsync(GpuProbeState state)
    {
        var dump = Environment.GetEnvironmentVariable("PDFVIEWERLITE_GPU_PROBE_CPU_DUMP");
        if (!string.IsNullOrEmpty(dump) && state.CpuTilePixels is { } pixels)
        {
            await File.WriteAllBytesAsync(dump, pixels).ConfigureAwait(false);
        }
    }

    /// <summary>Configures the private compositor probe for Wayland or X11.</summary>
    /// <returns>The configured application builder.</returns>
    private static AppBuilder CreateAppBuilder()
    {
        var builder = AppBuilder.Configure<GpuProbeApp>();
        if (Environment.GetEnvironmentVariable("PDFVIEWERLITE_GPU_PROBE_LOG") == "1")
        {
            builder = builder.LogToTextWriter(Console.Error, LogEventLevel.Verbose);
        }

        if (Environment.GetEnvironmentVariable(X11Variable) is { } x11Mode)
        {
            X11RenderingMode[] renderingModes = x11Mode switch
            {
                "vulkan" => [X11RenderingMode.Vulkan],
                "fallback" => [X11RenderingMode.Vulkan, X11RenderingMode.Egl, X11RenderingMode.Glx, X11RenderingMode.Software],
                "egl" => [X11RenderingMode.Egl],
                "glx" => [X11RenderingMode.Glx],
                "software" => [X11RenderingMode.Software],
                _ => [X11RenderingMode.Egl, X11RenderingMode.Glx, X11RenderingMode.Software],
            };
            _ = builder.UseX11().With(new X11PlatformOptions { RenderingMode = renderingModes });
            _ = builder.With(new VulkanOptions
            {
                VulkanInstanceCreationOptions = new() { VulkanVersion = new(1, VulkanMinorVersion, 0), InstanceExtensions = x11Mode == "fallback" ? [UnsupportedVulkanExtension] : [] },
            });
        }
        else
        {
            _ = builder.UseWayland().With(new WaylandPlatformOptions { WlDisplayName = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") });
        }

        return builder
            .UseTextShapingSubsystem(static () => SkiaPlatform.InitializeTextShaping(), "HarfBuzz")
            .UseRenderingSubsystem(static () => SkiaPlatform.Initialize(), "Skia");
    }

    /// <summary>Reads a file or builds a deterministic PDF workload.</summary>
    /// <param name="path">A file path or fixture name.</param>
    /// <returns>The PDF bytes.</returns>
    private static async Task<byte[]> ReadSourceAsync(string path) => path switch
    {
        "fixture:text" => RenderSamplePages.CreateTextPage(),
        "fixture:scan" => RenderSamplePages.CreateScanPage(),
        "fixture:transparency" => RenderSamplePages.CreateTransparencyPage(),
        _ => await File.ReadAllBytesAsync(path),
    };

    /// <summary>Reads an optional finite positive zoom scale.</summary>
    /// <param name="scale">The validated scale.</param>
    /// <returns>Whether the scale is usable.</returns>
    private static bool TryGetScale(out float scale)
    {
        var value = Environment.GetEnvironmentVariable(ScaleVariable);
        if (value is null)
        {
            scale = 1F;
            return true;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) && float.IsFinite(scale) && scale > 0;
    }

    /// <summary>Counts the PDF path and offsets without the optional comparison mode.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The source argument count.</returns>
    private static int CountSourceArguments(string[] args) =>
        args.Length > 0 && args[^1] is ToneParityOption or StandardsParityOption ? args.Length - 1 : args.Length;

    /// <summary>A minimal window that leases the same Skia context used for presentation.</summary>
    public sealed class GpuProbeApp : Application
    {
        /// <summary>Gets or sets the probe state before the window opens.</summary>
        internal static GpuProbeState? State { get; set; }

        /// <inheritdoc/>
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && State is { } state)
            {
                var window = new Window { Width = GpuProbeState.TileEdge, Height = GpuProbeState.TileEdge, Content = new GpuProbeControl(state) };
                desktop.MainWindow = window;
                _ = CloseIfNoFrameAsync(window, state);
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>Closes the probe if no graphics callback arrives.</summary>
        /// <param name="window">The probe window.</param>
        /// <param name="state">The probe state.</param>
        /// <returns>The monitor task.</returns>
        private static async Task CloseIfNoFrameAsync(Window window, GpuProbeState state)
        {
            const int TimeoutSeconds = 60;
            const int PollMilliseconds = 250;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(PollMilliseconds));
            try
            {
                while (await timer.WaitForNextTickAsync(timeout.Token))
                {
                    if (state.IsComplete)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (!state.IsComplete)
            {
                await Error.WriteLineAsync("No GPU render callback arrived within 60 seconds.");
                state.ExitCode = FailedExitCode;
                Dispatcher.UIThread.Post(window.Close);
            }
        }
    }

    /// <summary>Places one custom operation on Avalonia's real compositor surface.</summary>
    /// <param name="state">The prepared PDF.</param>
    internal sealed class GpuProbeControl(GpuProbeState state) : Control
    {
        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.Custom(new GpuProbeOperation(state, this));
        }
    }

    /// <summary>Measures PDF replay after Avalonia makes its graphics context current.</summary>
    internal sealed class GpuProbeOperation : ICustomDrawOperation
    {
        /// <summary>The prepared PDF.</summary>
        private readonly GpuProbeState _state;

        /// <summary>The control whose window will close when the probe finishes.</summary>
        private readonly GpuProbeControl _control;

        /// <summary>Initializes a new instance of the <see cref="GpuProbeOperation"/> class.</summary>
        /// <param name="state">The prepared PDF.</param>
        /// <param name="control">The presenting control.</param>
        internal GpuProbeOperation(GpuProbeState state, GpuProbeControl control)
        {
            _state = state;
            _control = control;
        }

        /// <inheritdoc/>
        public Rect Bounds => new(0, 0, GpuProbeState.TileEdge, GpuProbeState.TileEdge);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HitTest(Point p) => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public void Render(ImmediateDrawingContext context)
        {
            if (!_state.TryStart())
            {
                return;
            }

            try
            {
                if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
                {
                    throw new InvalidOperationException("The compositor did not expose a Skia lease.");
                }

                using var lease = feature.Lease();
                if (lease.GrContext is not { } gpu)
                {
                    throw new InvalidOperationException("The compositor is using software rendering.");
                }

                _state.Measure(gpu, lease.SkCanvas, lease.SurfaceColorType);
            }
            catch (Exception exception)
            {
                Error.WriteLine(exception);
                _state.ExitCode = FailedExitCode;
            }
            finally
            {
                _state.Complete();
                if (TopLevel.GetTopLevel(_control) is Window window)
                {
                    Dispatcher.UIThread.Post(window.Close);
                }
            }
        }
    }

    /// <summary>Holds one PDF and collects completed CPU and GPU timings on the compositor thread.</summary>
    /// <param name="renderer">The renderer with a prepared first page.</param>
    /// <param name="request">The first tile request.</param>
    /// <param name="openTime">The document open time.</param>
    /// <param name="recordTime">The first page recording time.</param>
    /// <param name="toneParity">Whether to measure and compare GPU page tone output.</param>
    /// <param name="standardsParity">Whether to compare generated standards fixtures on GPU and CPU.</param>
    internal sealed class GpuProbeState(PdfPageRenderer renderer, PdfTileRequest request, TimeSpan openTime, TimeSpan recordTime, bool toneParity, bool standardsParity)
    {
        /// <summary>The measured tile edge.</summary>
        internal const int TileEdge = 512;

        /// <summary>The number of repetitions after first use.</summary>
        private const int Repetitions = 100;

        /// <summary>The number of BGRA bytes in one pixel.</summary>
        private const int BytesPerPixel = 4;

        /// <summary>Milliseconds in one second.</summary>
        private const int MillisecondsPerSecond = 1000;

        /// <summary>Writes completed probe measurements.</summary>
        private static readonly TextWriter Output = Console.Out;

        /// <summary>Whether the callback has started.</summary>
        private int _started;

        /// <summary>Whether the callback has finished.</summary>
        private int _completed;

        /// <summary>Gets whether the callback finished.</summary>
        internal bool IsComplete => Volatile.Read(ref _completed) != 0;

        /// <summary>Gets or sets the process exit code.</summary>
        internal int ExitCode { get; set; }

        /// <summary>Gets the CPU pixels retained only when a diagnostic dump was requested.</summary>
        internal byte[]? CpuTilePixels { get; private set; }

        /// <summary>Starts measuring once.</summary>
        /// <returns>Whether this is the first callback.</returns>
        internal bool TryStart() => Interlocked.Exchange(ref _started, 1) == 0;

        /// <summary>Marks the callback complete.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Complete() => Volatile.Write(ref _completed, 1);

        /// <summary>Measures GPU completion and matching CPU work with an already prepared page.</summary>
        /// <param name="gpu">The context shared with Avalonia presentation.</param>
        /// <param name="canvas">The compositor canvas.</param>
        /// <param name="colorType">The compositor's channel order.</param>
        /// <exception cref="InvalidOperationException">A GPU target cannot be created or a replay fails.</exception>
        internal void Measure(GRContext gpu, SKCanvas canvas, SKColorType colorType)
        {
            Output.WriteLine($"gpu_backend={gpu.Backend}");
            using var colorSpace = SKColorSpace.CreateSrgb();
            var channelOrder = colorType is SKColorType.Bgra8888 or SKColorType.Rgba8888 ? colorType : SKColorType.Bgra8888;
            if (standardsParity)
            {
                StandardsParityProbe.Run(gpu, channelOrder, colorSpace, Output);
                return;
            }

            using var target = CreateTarget(gpu, colorSpace, ref channelOrder);
            gpu.GetResourceCacheUsage(out var resourcesBefore, out var cacheBefore);
            var before = new GpuCacheUsage(resourcesBefore, cacheBefore);
            GpuProbeEventScope.Start("FirstGpu", 1);
            var firstStart = Stopwatch.GetTimestamp();
            if (!renderer.RenderToTarget(request, target))
            {
                throw new InvalidOperationException("First GPU tile replay failed.");
            }

            gpu.Flush(true, true);
            var firstGpu = Stopwatch.GetElapsedTime(firstStart);
            GpuProbeEventScope.Stop("FirstGpu");
            gpu.GetResourceCacheUsage(out var resourcesAfterFirst, out var bytesAfterFirst);
            var gpuSamples = SampleWarmGpu(gpu, target, renderer, request);
            gpu.GetResourceCacheUsage(out var resourcesAfterWarm, out var bytesAfterWarm);
            using var image = target.Snapshot();
            var drawSamples = SampleCachedDraw(gpu, canvas, image);
            gpu.GetResourceCacheUsage(out var resourcesAfter, out var cacheAfter);
            var after = new GpuCacheUsage(resourcesAfter, cacheAfter);
            var cpuSamples = SampleCpu();
            Report(channelOrder, before, after, firstGpu, gpuSamples, drawSamples, cpuSamples);
            Output.WriteLine($"gpu_resources_after_first={resourcesAfterFirst} gpu_cache_after_first_bytes={bytesAfterFirst}");
            Output.WriteLine($"gpu_resources_after_warm={resourcesAfterWarm} gpu_cache_after_warm_bytes={bytesAfterWarm}");
            SampleNewGpuTile(gpu, colorSpace, channelOrder);
            ReportCpuTileHash();
            if (toneParity)
            {
                ToneParityProbe.Run(gpu, image, channelOrder, colorSpace, Output);
            }
        }

        /// <summary>Creates a surface compatible with the presentation context.</summary>
        /// <param name="gpu">The shared graphics context.</param>
        /// <param name="colorSpace">The sRGB colour space.</param>
        /// <param name="channelOrder">The preferred channel order, updated when the alternate succeeds.</param>
        /// <returns>The owned target.</returns>
        /// <exception cref="InvalidOperationException">Neither colour layout can be allocated.</exception>
        private static SkiaSurfaceRenderTarget CreateTarget(GRContext gpu, SKColorSpace colorSpace, ref SKColorType channelOrder)
        {
            var info = new SKImageInfo(TileEdge, TileEdge, channelOrder, SKAlphaType.Premul, colorSpace);
            var surface = TryCreateSurface(gpu, info);
            if (surface is null)
            {
                channelOrder = channelOrder == SKColorType.Bgra8888 ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
                info = new(TileEdge, TileEdge, channelOrder, SKAlphaType.Premul, colorSpace);
                surface = TryCreateSurface(gpu, info) ?? throw new InvalidOperationException("The shared device cannot create a 512-pixel GPU tile.");
            }

            return new(surface, info);
        }

        /// <summary>Attempts an offscreen GPU surface without assuming every driver supports the layout.</summary>
        /// <param name="gpu">The shared graphics context.</param>
        /// <param name="info">The requested layout.</param>
        /// <returns>The GPU surface or null.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static SKSurface? TryCreateSurface(GRContext gpu, SKImageInfo info)
        {
            try
            {
                return SKSurface.Create(gpu, false, info);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
            {
                return null;
            }
        }

        /// <summary>Measures replay and synchronous GPU completion after first upload.</summary>
        /// <param name="gpu">The shared graphics context.</param>
        /// <param name="target">The retained GPU target.</param>
        /// <param name="renderer">The prepared renderer.</param>
        /// <param name="request">The tile to replay.</param>
        /// <returns>The samples and managed allocation count.</returns>
        /// <exception cref="InvalidOperationException">A replay fails.</exception>
        private static ProbeSamples SampleWarmGpu(GRContext gpu, SkiaSurfaceRenderTarget target, PdfPageRenderer renderer, PdfTileRequest request)
        {
            var gpuTicks = new long[Repetitions];
            var submissionTicks = new long[Repetitions];
            GpuProbeEventScope.Start("WarmGpuReplay", Repetitions);
            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < gpuTicks.Length; index++)
            {
                var start = Stopwatch.GetTimestamp();
                if (!renderer.RenderToTarget(request, target))
                {
                    throw new InvalidOperationException("Warm GPU tile replay failed.");
                }

                submissionTicks[index] = Stopwatch.GetTimestamp() - start;
                gpu.Flush(true, true);
                gpuTicks[index] = Stopwatch.GetTimestamp() - start;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            GpuProbeEventScope.Stop("WarmGpuReplay");
            Output.WriteLine($"warm_gpu_submission_mean_ms={MeanMilliseconds(submissionTicks):F3}");
            return new(gpuTicks, allocated);
        }

        /// <summary>Measures drawing a retained GPU image into Avalonia's compositor canvas.</summary>
        /// <param name="gpu">The shared graphics context.</param>
        /// <param name="canvas">The compositor canvas.</param>
        /// <param name="image">The retained GPU image.</param>
        /// <returns>The samples and managed allocation count.</returns>
        private static ProbeSamples SampleCachedDraw(GRContext gpu, SKCanvas canvas, SKImage image)
        {
            var destination = new SKRect(0, 0, TileEdge, TileEdge);
            var source = new SKRect(0, 0, TileEdge, TileEdge);
            var drawTicks = new long[Repetitions];
            GpuProbeEventScope.Start("CachedDraw", Repetitions);
            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < drawTicks.Length; index++)
            {
                var start = Stopwatch.GetTimestamp();
                canvas.DrawImage(image, source, destination, new SKSamplingOptions(SKFilterMode.Nearest));
                gpu.Flush(true, true);
                drawTicks[index] = Stopwatch.GetTimestamp() - start;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            GpuProbeEventScope.Stop("CachedDraw");
            return new(drawTicks, allocated);
        }

        /// <summary>Converts completed operation ticks to mean milliseconds.</summary>
        /// <param name="ticks">The elapsed timestamps.</param>
        /// <returns>The mean milliseconds.</returns>
        private static double MeanMilliseconds(long[] ticks)
        {
            long total = 0;
            foreach (var sample in ticks)
            {
                total += sample;
            }

            return (double)total * MillisecondsPerSecond / Stopwatch.Frequency / ticks.Length;
        }

        /// <summary>Measures a fresh GPU target and snapshot after image resources have warmed.</summary>
        /// <param name="gpu">The shared graphics context.</param>
        /// <param name="colorSpace">The output colour space.</param>
        /// <param name="channelOrder">The preferred GPU layout.</param>
        /// <exception cref="InvalidOperationException">The tile does not render.</exception>
        private void SampleNewGpuTile(GRContext gpu, SKColorSpace colorSpace, SKColorType channelOrder)
        {
            GpuProbeEventScope.Start("NewGpuTile", Repetitions);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var index = 0; index < Repetitions; index++)
            {
                using var target = CreateTarget(gpu, colorSpace, ref channelOrder);
                if (!renderer.RenderToTarget(request, target))
                {
                    throw new InvalidOperationException("The new GPU tile did not render.");
                }

                using var image = target.Snapshot();
                gpu.Flush(true, true);
            }

            var elapsed = Stopwatch.GetElapsedTime(start);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            GpuProbeEventScope.Stop("NewGpuTile");
            Output.WriteLine($"new_gpu_tile_completed_mean_ms={elapsed.TotalMilliseconds / Repetitions:F3} new_gpu_tile_managed_bytes_per_tile={allocated / Repetitions}");
        }

        /// <summary>Measures the matching warm CPU tile on the compositor thread.</summary>
        /// <returns>The samples and managed allocation count.</returns>
        /// <exception cref="InvalidOperationException">A CPU replay fails.</exception>
        private ProbeSamples SampleCpu()
        {
            var pixels = new byte[TileEdge * TileEdge * BytesPerPixel];
            _ = renderer.Render(request, new(pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel));
            var cpuTicks = new long[Repetitions];
            var cpuAllocationStart = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < cpuTicks.Length; index++)
            {
                var start = Stopwatch.GetTimestamp();
                if (!renderer.Render(request, new(pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel)))
                {
                    throw new InvalidOperationException("CPU tile replay failed.");
                }

                cpuTicks[index] = Stopwatch.GetTimestamp() - start;
            }

            return new(cpuTicks, GC.GetAllocatedBytesForCurrentThread() - cpuAllocationStart);
        }

        /// <summary>Identifies the exact software pixels for comparisons between renderer variants.</summary>
        /// <exception cref="InvalidOperationException">The comparison tile could not be rendered.</exception>
        private void ReportCpuTileHash()
        {
            var pixels = new byte[TileEdge * TileEdge * BytesPerPixel];
            if (!renderer.Render(request, new(pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel)))
            {
                throw new InvalidOperationException("The comparison CPU tile did not render.");
            }

            Output.WriteLine($"cpu_tile_sha256={Convert.ToHexString(SHA256.HashData(pixels))}");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PDFVIEWERLITE_GPU_PROBE_CPU_DUMP")))
            {
                CpuTilePixels = pixels;
            }
        }

        /// <summary>Reports completed timings and the limits of the probe.</summary>
        /// <param name="channelOrder">The GPU tile's channel order.</param>
        /// <param name="before">Skia cache usage before the tile.</param>
        /// <param name="after">Skia cache usage after the tile.</param>
        /// <param name="firstGpu">The first GPU replay including upload and completion.</param>
        /// <param name="gpuSamples">Completed warm GPU replays.</param>
        /// <param name="drawSamples">Completed cached image draws.</param>
        /// <param name="cpuSamples">Warm CPU replays.</param>
        private void Report(
            SKColorType channelOrder,
            GpuCacheUsage before,
            GpuCacheUsage after,
            TimeSpan firstGpu,
            ProbeSamples gpuSamples,
            ProbeSamples drawSamples,
            ProbeSamples cpuSamples)
        {
            Output.WriteLine($"device=shared-avalonia-skia channel={channelOrder} gpu_resources_before={before.Resources} gpu_cache_before_bytes={before.Bytes}");
            Output.WriteLine($"request_scale={request.Scale:F3} request_offset_x={request.OffsetX} request_offset_y={request.OffsetY}");
            Output.WriteLine($"gpu_resources_after={after.Resources} gpu_cache_after_bytes={after.Bytes}");
            Output.WriteLine($"open_ms={openTime.TotalMilliseconds:F3} record_ms={recordTime.TotalMilliseconds:F3} first_gpu_replay_completed_ms={firstGpu.TotalMilliseconds:F3}");
            Output.WriteLine($"warm_gpu_replay_completed_mean_ms={MeanMilliseconds(gpuSamples.Ticks):F3} warm_gpu_managed_bytes_per_op={(double)gpuSamples.ManagedBytes / Repetitions:F1}");
            Output.WriteLine($"cached_image_draw_completed_mean_ms={MeanMilliseconds(drawSamples.Ticks):F3} cpu_replay_mean_ms={MeanMilliseconds(cpuSamples.Ticks):F3}");
            Output.WriteLine($"cached_draw_managed_bytes_per_op={(double)drawSamples.ManagedBytes / Repetitions:F1} cpu_managed_bytes_per_op={(double)cpuSamples.ManagedBytes / Repetitions:F1}");
            Output.WriteLine($"logical_tile_bytes={TileEdge * TileEdge * BytesPerPixel} measured_gpu_upload_bytes=unavailable gpu_readback_bytes=0 frame_present_latency=unavailable");
        }

        /// <summary>One sample group and the managed bytes allocated while collecting it.</summary>
        /// <param name="Ticks">The elapsed ticks per operation.</param>
        /// <param name="ManagedBytes">Managed bytes for the whole group.</param>
        private readonly record struct ProbeSamples(long[] Ticks, long ManagedBytes);

        /// <summary>Skia's reported GPU resource cache usage.</summary>
        /// <param name="Resources">The number of cached GPU resources.</param>
        /// <param name="Bytes">The bytes charged to those resources.</param>
        private readonly record struct GpuCacheUsage(int Resources, long Bytes);
    }
}
