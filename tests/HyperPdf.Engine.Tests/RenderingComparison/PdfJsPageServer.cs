// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Serves verified pdf.js modules and receives actual canvas pixels on loopback.</summary>
internal sealed class PdfJsPageServer : IAsyncDisposable
{
    /// <summary>The maximum accepted pixel response length.</summary>
    private const int MaximumResponseBytes = 256 * 1024 * 1024;

    /// <summary>The maximum asynchronous cleanup wait.</summary>
    private const int CleanupSeconds = 30;

    /// <summary>The maximum wait for one HTTP body or response.</summary>
    private const int RequestSeconds = 30;

    /// <summary>The checksum-verified browser assets.</summary>
    private readonly Dictionary<string, byte[]> _assets;

    /// <summary>The owned cross-platform loopback HTTP server.</summary>
    private readonly WebApplication _application;

    /// <summary>Cancels owned background operations.</summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>Protects published render request state.</summary>
    private readonly Lock _gate = new();

    /// <summary>The owned PDF for the active request.</summary>
    private ReadOnlyMemory<byte> _pdf;

    /// <summary>Completes the active render request.</summary>
    private TaskCompletionSource<PdfJsPageResponse>? _pending;

    /// <summary>Identifies the active render request.</summary>
    private string _job = string.Empty;

    /// <summary>Tracks completed lifetime teardown.</summary>
    private int _disposed;

    /// <summary>Cancels the current render's HTTP operations.</summary>
    private CancellationTokenSource? _request;

    /// <summary>Initializes a new instance of the <see cref="PdfJsPageServer"/> class.</summary>
    /// <param name="assets">The verified browser distribution.</param>
    /// <param name="application">The owned loopback application.</param>
    private PdfJsPageServer(Dictionary<string, byte[]> assets, WebApplication application)
    {
        _assets = assets;
        _assets.Add("/index.html", BrowserPage.ToArray());
        _application = application;
    }

    /// <summary>Gets the actual dynamically bound loopback origin.</summary>
    internal string BaseUrl { get; private set; } = string.Empty;

    /// <summary>Gets the pdf.js browser module that renders and reports canvas pixels.</summary>
    private static ReadOnlySpan<byte> BrowserPage => """
        <!doctype html><meta charset="utf-8"><canvas id="page"></canvas><script type="module">
        const query=new URLSearchParams(location.search), job=query.get('job');
        try {
          const pdfjs=await import('/build/pdf.mjs');
          pdfjs.GlobalWorkerOptions.workerSrc='/build/pdf.worker.mjs';
          const pdf=await pdfjs.getDocument({url:'/fixture.pdf?job='+job,cMapUrl:'/web/cmaps/',cMapPacked:true,
            standardFontDataUrl:'/web/standard_fonts/',wasmUrl:'/web/wasm/',iccUrl:'/web/iccs/'}).promise;
          const pageIndex=Number(query.get('page')),scale=Number(query.get('scale'));
          const page=await pdf.getPage(pageIndex+1),viewport=page.getViewport({scale});
          const canvas=document.getElementById('page');
          canvas.width=Math.ceil(viewport.width);canvas.height=Math.ceil(viewport.height);
          const context=canvas.getContext('2d',{willReadFrequently:true});
          await page.render({canvasContext:context,viewport,background:'rgb(255,255,255)'}).promise;
          const metadata={version:pdfjs.version,width:canvas.width,height:canvas.height,pageIndex,scale,
            rotation:viewport.rotation,viewportWidth:viewport.width,viewportHeight:viewport.height,
            viewportTransform:viewport.transform,pageView:page.view,userUnit:page.userUnit,
            devicePixelRatio:window.devicePixelRatio};
          await fetch('/result?job='+job,{method:'POST',headers:{'X-Pdfjs-Metadata':JSON.stringify(metadata)},
            body:context.getImageData(0,0,canvas.width,canvas.height).data});
          await pdf.destroy();
        } catch(error) {
          await fetch('/error?job='+job,{method:'POST',body:String(error)+'\n'+error.stack});
        }
        </script>
        """u8;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _lifetime.CancelAsync();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
            await _application.StopAsync(cleanup.Token);
            await _application.DisposeAsync().AsTask().WaitAsync(cleanup.Token);
        }
        finally
        {
            _request?.Dispose();
            _lifetime.Dispose();
        }
    }

    /// <summary>Starts a cross-platform server on an actual ephemeral loopback port.</summary>
    /// <param name="assets">The checksum-verified browser files.</param>
    /// <param name="cancellationToken">Cancels server startup.</param>
    /// <returns>The owned server after its listener is ready.</returns>
    internal static async Task<PdfJsPageServer> CreateAsync(Dictionary<string, byte[]> assets, CancellationToken cancellationToken)
    {
        var application = CreateBuilder(Path.GetTempPath()).Build();
        var server = new PdfJsPageServer(assets, application);
        application.Run(server.RespondAsync);
        try
        {
            await application.StartAsync(cancellationToken);
            server.BaseUrl = application.Urls.Single();
            return server;
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    /// <summary>Configures the in-memory asset server without filesystem configuration reloads.</summary>
    /// <param name="contentRoot">The host's content root, whose files are not loaded or watched.</param>
    /// <returns>The explicitly configured loopback application builder.</returns>
    internal static WebApplicationBuilder CreateBuilder(string contentRoot)
    {
        // Slim defaults watch appsettings files recursively under the content root, even when absent.
        // This server uses only verified in-memory assets and has no file configuration to reload.
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = contentRoot, Args = [] });
        _ = builder.WebHost.UseKestrelCore();
        _ = builder.WebHost.ConfigureKestrel(static options =>
        {
            options.Listen(IPAddress.Loopback, 0);
            options.Limits.MaxRequestBodySize = MaximumResponseBytes;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(RequestSeconds);
        });
        return builder;
    }

    /// <summary>Publishes an owned PDF for one render and returns its completion task.</summary>
    /// <param name="pdf">The PDF snapshot.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="scale">Pixels per PDF point.</param>
    /// <param name="cancellationToken">Cancels the current render's HTTP operations.</param>
    /// <param name="url">The isolated browser page to navigate.</param>
    /// <returns>The actual canvas response.</returns>
    internal Task<PdfJsPageResponse> BeginRender(ReadOnlyMemory<byte> pdf, int pageIndex, double scale, CancellationToken cancellationToken, out string url)
    {
        Task<PdfJsPageResponse> completion;
        lock (_gate)
        {
            _request?.Dispose();
            _request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            _pdf = pdf.ToArray();
            _job = Guid.NewGuid().ToString("N");
            _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            url = $"{BaseUrl}/index.html?job={_job}&page={pageIndex.ToString(CultureInfo.InvariantCulture)}&scale={scale.ToString("R", CultureInfo.InvariantCulture)}";
            completion = _pending.Task;
        }

        return completion;
    }

    /// <summary>Reads one bounded browser response body.</summary>
    /// <param name="request">The incoming browser request.</param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>The complete body bytes.</returns>
    /// <exception cref="InvalidDataException">The response length is invalid.</exception>
    private static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var length = request.ContentLength;
        if (length is null or < 0 or > MaximumResponseBytes)
        {
            throw new InvalidDataException("Invalid pdf.js response length.");
        }

        var bytes = new byte[checked((int)length.Value)];
        await request.Body.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    /// <summary>Serves one verified asset or receives a render response.</summary>
    /// <param name="context">The browser request.</param>
    /// <returns>A task.</returns>
    private async Task RespondAsync(HttpContext context)
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, context.RequestAborted);
        request.CancelAfter(TimeSpan.FromSeconds(RequestSeconds));
        try
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (HttpMethods.IsPost(context.Request.Method))
            {
                await ReceivePixelsAsync(context, path, request.Token);
            }
            else
            {
                await ServeAssetAsync(context, path, request.Token);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or OperationCanceledException)
        {
            lock (_gate)
            {
                if (context.Request.Query["job"] == _job)
                {
                    _ = _pending?.TrySetException(exception);
                }
            }

            context.Abort();
        }
    }

    /// <summary>Serves only an exact verified asset or the active owned PDF.</summary>
    /// <param name="context">The browser request.</param>
    /// <param name="path">The exact asset path.</param>
    /// <param name="cancellationToken">Cancels writing the response.</param>
    /// <returns>A task.</returns>
    private async Task ServeAssetAsync(HttpContext context, string path, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> bytes;
        lock (_gate)
        {
            if (path == "/fixture.pdf" && context.Request.Query["job"] == _job)
            {
                bytes = _pdf;
            }
            else if (_assets.TryGetValue(path, out var asset))
            {
                bytes = asset;
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
        }

        context.Response.ContentType = Path.GetExtension(path) switch
        {
            ".mjs" => "text/javascript",
            ".wasm" => "application/wasm",
            ".html" => "text/html",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, cancellationToken);
    }

    /// <summary>Completes the matching page request from actual browser pixels.</summary>
    /// <param name="context">The browser request.</param>
    /// <param name="path">The response kind.</param>
    /// <param name="cancellationToken">Cancels the request body.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidDataException">The browser response metadata is missing.</exception>
    private async Task ReceivePixelsAsync(HttpContext context, string path, CancellationToken cancellationToken)
    {
        TaskCompletionSource<PdfJsPageResponse>? pending;
        CancellationToken renderToken;
        lock (_gate)
        {
            pending = context.Request.Query["job"] == _job ? _pending : null;
            renderToken = _request?.Token ?? _lifetime.Token;
        }

        if (pending is null || (path != "/error" && path != "/result"))
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        using var body = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, renderToken);
        var bytes = await ReadBodyAsync(context.Request, body.Token);
        if (path == "/error")
        {
            _ = pending.TrySetException(new IOException($"pdf.js rendering failed: {Encoding.UTF8.GetString(bytes)}"));
            return;
        }

        var header = context.Request.Headers["X-Pdfjs-Metadata"].ToString();
        if (string.IsNullOrEmpty(header))
        {
            throw new InvalidDataException("Missing pdf.js metadata.");
        }

        using var metadata = JsonDocument.Parse(header);
        _ = pending.TrySetResult(new(bytes, metadata.RootElement.Clone()));
    }
}
