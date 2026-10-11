// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Reuses one isolated Firefox process for sequential actual pdf.js page renders.</summary>
internal sealed class PdfJsBrowserSession : IAsyncDisposable
{
    /// <summary>The maximum startup or render wait.</summary>
    private const int OperationSeconds = 180;

    /// <summary>The coordinate count in a page view box.</summary>
    private const int PageViewLength = 4;

    /// <summary>The component count in a viewport transform.</summary>
    private const int TransformLength = 6;

    /// <summary>The owned reusable browser.</summary>
    private readonly FirefoxBiDi _browser;

    /// <summary>The owned loopback page server.</summary>
    private readonly PdfJsPageServer _server;

    /// <summary>Serializes page renders and teardown.</summary>
    private readonly SemaphoreSlim _renderGate = new(1, 1);

    /// <summary>Tracks completed lifetime teardown.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfJsBrowserSession"/> class.</summary>
    /// <param name="browser">The browser.</param>
    /// <param name="server">The server.</param>
    private PdfJsBrowserSession(FirefoxBiDi browser, PdfJsPageServer server)
    {
        _browser = browser;
        _server = server;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(OperationSeconds));
        await _renderGate.WaitAsync(bounded.Token);
        try
        {
            await _browser.DisposeAsync();
        }
        finally
        {
            await _server.DisposeAsync();
            _ = _renderGate.Release();
        }
    }

    /// <summary>Starts a reusable browser with a temporary profile and verified renderer assets.</summary>
    /// <param name="cancellationToken">Cancels bounded startup and downloading.</param>
    /// <returns>The owned reference rendering session.</returns>
    /// <exception cref="FileNotFoundException">Firefox is not installed or configured.</exception>
    internal static async Task<PdfJsBrowserSession> CreateAsync(CancellationToken cancellationToken)
    {
        _ = FirefoxBrowserDiscovery.FindExecutable();
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(OperationSeconds));
        var assets = await PdfJsAssets.LoadAsync(bounded.Token);
        var server = await PdfJsPageServer.CreateAsync(assets, bounded.Token);
        try
        {
            return new(await OpenBrowserAsync(bounded.Token), server);
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    /// <summary>Renders one page on white and returns owned BGRA32 pixels and actual renderer metadata.</summary>
    /// <param name="pdf">The PDF bytes, copied before browser navigation.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="scale">Finite positive pixels per PDF point.</param>
    /// <param name="cancellationToken">Cancels bounded rendering.</param>
    /// <returns>The actual browser canvas raster and metadata.</returns>
    /// <exception cref="ArgumentException">The PDF is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page index or scale is invalid.</exception>
    /// <exception cref="IOException">The browser fails to render the page.</exception>
    internal async Task<PdfJsRenderResult> RenderAsync(ReadOnlyMemory<byte> pdf, int pageIndex, double scale, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        if (!double.IsFinite(scale))
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        if (pdf.IsEmpty)
        {
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));
        }

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(OperationSeconds));
        await _renderGate.WaitAsync(bounded.Token);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var completion = _server.BeginRender(pdf, pageIndex, scale, bounded.Token, out var url);
            await _browser.NavigateAsync(url, bounded.Token);
            var response = await completion.WaitAsync(bounded.Token);
            return ConvertResponse(response, pageIndex, scale);
        }
        catch (IOException exception)
        {
            throw new IOException($"pdf.js browser rendering failed: {_browser.Diagnostics}", exception);
        }
        finally
        {
            await bounded.CancelAsync();
            _ = _renderGate.Release();
        }
    }

    /// <summary>Retries one browser whose protocol startup timed out before the caller's deadline.</summary>
    /// <param name="cancellationToken">Cancels both attempts.</param>
    /// <returns>A ready browser.</returns>
    private static async Task<FirefoxBiDi> OpenBrowserAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await FirefoxBiDi.CreateAsync(cancellationToken);
        }
        catch (TimeoutException) when (!cancellationToken.IsCancellationRequested)
        {
            return await FirefoxBiDi.CreateAsync(cancellationToken);
        }
    }

    /// <summary>Converts opaque straight RGBA pixels into premultiplied BGRA.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <exception cref="InvalidDataException">A pixel is translucent despite the white page background.</exception>
    private static void ConvertPixels(byte[] pixels)
    {
        const int redChannel = 2;
        const int alphaChannel = 3;
        for (var offset = 0; offset < pixels.Length; offset += ComparisonRaster.BytesPerPixel)
        {
            var red = pixels[offset];
            pixels[offset] = pixels[offset + redChannel];
            pixels[offset + redChannel] = red;
            if (pixels[offset + alphaChannel] != byte.MaxValue)
            {
                throw new InvalidDataException("pdf.js returned translucent pixels despite a white page background.");
            }
        }
    }

    /// <summary>Reads an owned browser geometry vector of the required length.</summary>
    /// <param name="value">The metadata vector.</param>
    /// <param name="length">The required component count.</param>
    /// <returns>The owned vector.</returns>
    /// <exception cref="InvalidDataException">The geometry vector has the wrong length.</exception>
    private static ReadOnlyMemory<double> ReadVector(System.Text.Json.JsonElement value, int length)
    {
        var components = value.EnumerateArray().Select(static item => item.GetDouble()).ToArray();
        if (components.Length != length)
        {
            throw new InvalidDataException("pdf.js returned an invalid geometry vector.");
        }

        return components;
    }

    /// <summary>Validates page identity and converts actual browser pixels.</summary>
    /// <param name="response">The response.</param>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The operation result.</returns>
    /// <exception cref="InvalidDataException">The browser response does not match the render request.</exception>
    private PdfJsRenderResult ConvertResponse(PdfJsPageResponse response, int pageIndex, double scale)
    {
        var metadata = response.Metadata;
        var actualScale = BitConverter.DoubleToInt64Bits(metadata.GetProperty(nameof(scale)).GetDouble());
        if (metadata.GetProperty("version").GetString() != PdfJsAssets.Version
            || metadata.GetProperty(nameof(pageIndex)).GetInt32() != pageIndex
            || actualScale != BitConverter.DoubleToInt64Bits(scale))
        {
            throw new InvalidDataException("pdf.js returned a different renderer or page request.");
        }

        var width = metadata.GetProperty("width").GetInt32();
        var height = metadata.GetProperty("height").GetInt32();
        if (width <= 0 || height <= 0 || response.Pixels.Length != checked(width * height * ComparisonRaster.BytesPerPixel))
        {
            throw new InvalidDataException("pdf.js returned invalid pixel geometry.");
        }

        ConvertPixels(response.Pixels);
        var identity = new PdfJsRendererMetadata(
            PdfJsAssets.Version,
            _browser.Version,
            _browser.Build,
            pageIndex,
            scale,
            metadata.GetProperty("rotation").GetInt32(),
            metadata.GetProperty("viewportWidth").GetDouble(),
            metadata.GetProperty("viewportHeight").GetDouble(),
            metadata.GetProperty("devicePixelRatio").GetDouble(),
            metadata.GetProperty("userUnit").GetDouble(),
            ReadVector(metadata.GetProperty("pageView"), PageViewLength),
            ReadVector(metadata.GetProperty("viewportTransform"), TransformLength));
        return new(new ComparisonRaster(width, height, response.Pixels), identity);
    }
}
