// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// One optimisation run. <see cref="Prepare"/> checks what may change, edits a private working copy, plans images and
/// fonts, merges duplicates and builds the writer; the writer then calls back into <see cref="Transform"/> to re-encode
/// each image, font and stream as it is written. Used by one thread.
/// </summary>
[DebuggerDisplay("OptimizeSession")]
internal sealed partial class OptimizeSession : IObjectTransformer
{
    /// <summary>The version written when the document has none.</summary>
    private const string DefaultVersion = "1.4";

    /// <summary>The version object streams need.</summary>
    private const string ObjectStreamVersion = "1.5";

    /// <summary>The PDF/A part based on PDF 1.4, which forbids object streams.</summary>
    private const int PdfA1 = 1;

    /// <summary>The largest number of entries an idle session keeps in a collection; a session that grew past it is dropped when given back.</summary>
    private const int MaxKeptEntries = 4096;

    /// <summary>The registered service that owns this session's reusable state.</summary>
    private readonly OptimizerResources _resources;

    /// <summary>Collects what the run does.</summary>
    private readonly OptimizeReportBuilder _report = new();

    /// <summary>The image XObjects to re-encode, by old number, with how each is used.</summary>
    private readonly Dictionary<int, ImageUse> _images = [];

    /// <summary>The images other images use as soft or stencil masks, by old number.</summary>
    private readonly HashSet<int> _masks = [];

    /// <summary>Re-encodes images.</summary>
    private readonly ImageRecoder _recoder = new();

    /// <summary>The glyphs to keep in each embedded font program, by the program's old number.</summary>
    private FontPlanState? _fonts;

    /// <summary>The source document, never changed.</summary>
    private PdfDocument _source = null!;

    /// <summary>Whether the source is a private copy the session changes in place.</summary>
    private bool _ownsSource;

    /// <summary>The options.</summary>
    private PdfOptimizeOptions _options;

    /// <summary>Receives progress, or <see langword="null"/>.</summary>
    private IProgress<PdfOptimizeProgress>? _progress;

    /// <summary>Stops the run.</summary>
    private CancellationToken _cancellationToken;

    /// <summary>The private working copy, or <see langword="null"/> before it is opened.</summary>
    private PdfDocument? _working;

    /// <summary>1 once the session is disposed.</summary>
    private int _disposed = 1;

    /// <summary>What JBIG2 coding may be tried, after the PDF/A claim is taken into account.</summary>
    private PdfJbig2Mode _jbig2;

    /// <summary>What the analysis learned; leased for the run.</summary>
    private ContentUsageScanner? _scanner;

    /// <summary>The duplicates found; leased for the run.</summary>
    private DuplicateFinder? _duplicates;

    /// <summary>How the file is written; leased for the run.</summary>
    private WritePlan? _plan;

    /// <summary>The graph being planned, until ownership passes to the writer.</summary>
    private OptimizerGraph? _pendingGraph;

    /// <summary>Initializes a new instance of the <see cref="OptimizeSession"/> class.</summary>
    /// <param name="resources">The registered service's reusable state.</param>
    private OptimizeSession(OptimizerResources resources) => _resources = resources;

    /// <summary>Gets the working copy.</summary>
    private PdfDocument Working => _working!;

    /// <summary>Takes an idle session, or makes one, and points it at a document.</summary>
    /// <param name="source">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="progress">Receives progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The session, which the caller disposes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OptimizeSession Rent(PdfDocument source, in PdfOptimizeOptions options, IProgress<PdfOptimizeProgress>? progress, CancellationToken cancellationToken) =>
        Rent(source, options, progress, false, cancellationToken);

    /// <summary>Takes an idle session, or makes one, and points it at a document.</summary>
    /// <param name="source">The document, or a private copy the session may change.</param>
    /// <param name="options">The options.</param>
    /// <param name="progress">Receives progress, or <see langword="null"/>.</param>
    /// <param name="ownsSource">Whether <paramref name="source"/> is already a private copy to change in place, so no second copy is made.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The session, which the caller disposes.</returns>
    internal static OptimizeSession Rent(PdfDocument source, in PdfOptimizeOptions options, IProgress<PdfOptimizeProgress>? progress, bool ownsSource, CancellationToken cancellationToken)
    {
        var resources = source.DrawingServices.Optimization;
        var session = resources.Sessions.Take() ?? new(resources);
        session._ownsSource = ownsSource;
        session._source = source;
        session._options = options;
        session._progress = progress;
        session._cancellationToken = cancellationToken;
        _ = Interlocked.Exchange(ref session._disposed, 0);
        return session;
    }

    /// <summary>
    /// Disposes the working copy the session opened, once, and gives the session back to the pool. A copy the caller
    /// supplied is the caller's to dispose, and the copy borrows the source's security handler and file, so the source stays usable.
    /// </summary>
    internal void Return()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var grew = _images.Count > MaxKeptEntries;
        try
        {
            if (!_ownsSource)
            {
                _working?.Dispose();
            }
        }
        finally
        {
            ReleaseRun();
            if (!grew)
            {
                _ = _resources.Sessions.Offer(this);
            }
        }
    }

    /// <summary>Decides what may change, makes the changes that are not made while writing, and plans the write.</summary>
    /// <returns>The plan.</returns>
    /// <exception cref="PdfException">The document cannot be read.</exception>
    internal WritePlan Prepare()
    {
        Report(PdfOptimizePhase.Checking, 0, 0);
        _working = _ownsSource ? _source : PdfDocumentOptimizing.OpenWorkingCopy(_source);
        _report.PdfAPart = PdfDocumentMetadata.GetXmp(Working)?.PdfAPart ?? 0;
        _jbig2 = Jbig2Mode();

        // PDF/A-1 forbids JPEG 2000 images (ISO 19005-1 6.1.10).
        _recoder.Configure(_options, Working.DrawingServices, _source.Objects, _report.PdfAPart != PdfA1, _jbig2, _cancellationToken);
        _report.WasSigned = IsSigned(Working);
        if (_report.WasSigned)
        {
            _plan = PrepareSigned();
            return _plan;
        }

        var scanner = Analyse();
        Edit(scanner);
        _plan = PlanRewrite(scanner);
        return _plan;
    }

    /// <summary>Builds the report once the file is written.</summary>
    /// <param name="written">The bytes written.</param>
    /// <param name="plan">How the file was written, with the check of its PDF/A claim.</param>
    /// <returns>The report.</returns>
    internal PdfOptimizeReport Finish(long written, WritePlan plan)
    {
        Report(PdfOptimizePhase.Done, 1, 1);
        _report.PdfAClaim = plan.Claim;
        _report.PdfUaClaim = plan.UaClaim;
        return _report.Build(_source.Objects.Source.Length, written, plan.Mode);
    }

    /// <summary>Determines whether a document has a signature: a filled signature field, or usage rights or modification detection permissions.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when it is signed.</returns>
    private static bool IsSigned(PdfDocument document) =>
        PdfDocumentAttachments.GetSignatures(document).Count > 0 || SignaturePresence.HasSignature(document.Objects);

    /// <summary>Compares two PDF versions such as "1.4" and "2.0".</summary>
    /// <param name="version">The version.</param>
    /// <returns>The version as a number, or zero.</returns>
    private static double VersionNumber(string version) => double.TryParse(version, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ? number : 0;

    /// <summary>Determines whether an object is file structure the rewrite replaces rather than drops.</summary>
    /// <param name="value">The object.</param>
    /// <returns><see langword="true"/> for cross-reference and object streams.</returns>
    private static bool IsStructural(PdfValue value) =>
        value.AsStream()?.Dictionary is { } dictionary
        && (dictionary.IsName(
        KnownName.Type,
        KnownName.XRef) || dictionary.IsName(
        KnownName.Type,
        KnownName.ObjStm));

    /// <summary>Plans a signed document: only additions, appended as an incremental update, or an unchanged copy.</summary>
    /// <returns>The plan.</returns>
    private WritePlan PrepareSigned()
    {
        const string Reason = "The document is signed, so it is not rewritten and no image, font or stream is changed.";
        _report.Skip(PdfOptimizeCategory.Safety, 0, Reason);
        _report.IsEncrypted = Working.IsEncrypted;
        if (!_options.AllowIncrementalWhenSigned)
        {
            return WritePlan.Copy(_source.Objects.Source, _resources);
        }

        AddTextLayers();
        if (!StoreEditing.HasEdits(Working.Objects))
        {
            return WritePlan.Copy(_source.Objects.Source, _resources);
        }

        _report.Noted(PdfOptimizeCategory.Safety, 0, "Additions were appended as an incremental update, so existing signatures stay valid.");
        var plan = WritePlan.Incremental(Working.Objects, _resources);
        plan.SetUnmetUaClaim(_options.UnmetPdfUaClaim);
        return plan;
    }

    /// <summary>Reads the pages to learn how images and fonts are used.</summary>
    /// <returns>The scanner holding what was learned.</returns>
    private ContentUsageScanner Analyse()
    {
        var scanner = ContentUsageScanner.Rent(Working, _cancellationToken);
        _scanner = scanner;
        Report(PdfOptimizePhase.Analysing, 0, Working.PageCount);
        scanner.ScanDocument(_progress, _cancellationToken);
        return scanner;
    }

    /// <summary>Makes the page changes: cleanup, accessibility entries, text layers and inferred tags.</summary>
    /// <param name="scanner">What the analysis learned.</param>
    private void Edit(ContentUsageScanner scanner)
    {
        Report(PdfOptimizePhase.Editing, 0, 0);
        if (_options.Cleanup != PdfCleanupItems.None)
        {
            CleanupPass.Run(Working, scanner, _options.Cleanup, _report);
        }

        AddTextLayers();
        if (_options.AddInferredTags)
        {
            InferredTagger.Run(Working, _options, _report, _source.Objects.ScratchPools, _cancellationToken);
        }

        if (_options.FixAccessibility)
        {
            AccessibilityPass.Run(Working, _options, _report);
        }

        ShareJbig2Dictionary(scanner);
    }

    /// <summary>Codes images that share marks with one JBIG2 dictionary, when JBIG2 may be used and that is smaller.</summary>
    /// <param name="scanner">What the analysis learned.</param>
    private void ShareJbig2Dictionary(ContentUsageScanner scanner)
    {
        if (_jbig2 == PdfJbig2Mode.Off)
        {
            return;
        }

        var mode = _jbig2 == PdfJbig2Mode.Lossy && _options.AllowLossyImages ? PdfJbig2Mode.Lossy : PdfJbig2Mode.Lossless;
        SharedJbig2Pass.Run(Working.Objects, scanner, mode, _report, _cancellationToken);
    }

    /// <summary>Adds text layers from the recognition hook to image-only pages, unless a PDF/A claim forbids the unembedded font.</summary>
    private void AddTextLayers()
    {
        if (_options.OcrWords is not { } ocr)
        {
            return;
        }

        if (_report.PdfAPart > 0)
        {
            _report.Skip(PdfOptimizeCategory.TextLayer, 0, "Text layers use a font that is not embedded, which PDF/A forbids, so none were added.");
            return;
        }

        OcrLayerPass.Run(Working, ocr, _report, _cancellationToken);
    }

    /// <summary>Plans the rewrite: images, fonts, duplicates and the layout.</summary>
    /// <param name="scanner">What the analysis learned.</param>
    /// <returns>The plan.</returns>
    private WritePlan PlanRewrite(ContentUsageScanner scanner)
    {
        var store = Working.Objects;
        var keepEncryption = store.Security is not null && !_options.RemoveEncryption;
        if (keepEncryption)
        {
            // The optimizer writes the file in pieces and has no way to place a PDF MAC over the whole of it.
            PdfMacSeal.ThrowIfNeeded(store, "Optimizing");
        }

        _report.IsEncrypted = keepEncryption;
        if (store.Security is not null && _options.RemoveEncryption)
        {
            _report.Noted(PdfOptimizeCategory.Safety, 0, "Encryption was removed, as the options asked.");
        }

        var reached = OptimizerGraph.Collect(store, keepEncryption, null, _resources);
        _pendingGraph = reached;
        PlanImages(scanner, reached);
        if (_options.SubsetFonts)
        {
            _fonts = FontPlanState.Rent(_resources);
            FontSubsetPlanner.Plan(Working, scanner, reached, _fonts, _report, _cancellationToken);
        }

        CountUnused(reached);
        Report(PdfOptimizePhase.Deduplicating, 0, 0);
        var graph = reached;
        if (_options.RemoveDuplicates)
        {
            _duplicates = DuplicateFinder.Find(store, reached, _report, _cancellationToken);
            MergeAliasedPlans(_duplicates.Aliases);
            graph = OptimizerGraph.Collect(store, keepEncryption, _duplicates.Aliases, _resources);
            _pendingGraph = graph;
            reached.Return();
        }

        var settings = new WriteSettings(UseObjectStreams(), keepEncryption, Version(store));
        Report(PdfOptimizePhase.Writing, 0, graph.Count);
        var plan = WritePlan.Rewrite(OptimizedFileWriter.Rent(graph, store, settings, this));
        _pendingGraph = null;
        plan.SetUnmetUaClaim(_options.UnmetPdfUaClaim);
        return plan;
    }

    /// <summary>Decides how JBIG2 may be used: not at all in a PDF/A-1 file, which forbids it.</summary>
    /// <returns>The JBIG2 mode for black-and-white images.</returns>
    private PdfJbig2Mode Jbig2Mode()
    {
        if (_options.Jbig2 == PdfJbig2Mode.Off || !_options.OptimizeImages)
        {
            return PdfJbig2Mode.Off;
        }

        if (_report.PdfAPart != PdfA1)
        {
            return _options.Jbig2;
        }

        _report.Skip(PdfOptimizeCategory.Images, 0, "The document claims PDF/A-1, which forbids JBIG2, so black-and-white images were not coded as JBIG2.");
        return PdfJbig2Mode.Off;
    }

    /// <summary>Decides whether to write object streams, which PDF/A-1 forbids.</summary>
    /// <returns><see langword="true"/> when they are written.</returns>
    private bool UseObjectStreams()
    {
        if (!_options.UseObjectStreams)
        {
            return false;
        }

        if (_report.PdfAPart == PdfA1)
        {
            _report.Skip(PdfOptimizeCategory.Layout, 0, "The document claims PDF/A-1, which forbids object streams, so a classic cross-reference table was kept.");
            return false;
        }

        return true;
    }

    /// <summary>Gets the header version: the source's, raised for object streams, never lowered.</summary>
    /// <param name="store">The working store.</param>
    /// <returns>The version.</returns>
    private string Version(PdfObjectStore store)
    {
        var version = store.Version.Length == 0 ? DefaultVersion : store.Version;
        var required = RequiredVersion();
        return required is not null && VersionNumber(version) < VersionNumber(required) ? required : version;
    }

    /// <summary>Gets the oldest header version the optimised file may need, for the features the options allow.</summary>
    /// <returns>The version, or <see langword="null"/> when no feature needs a newer header.</returns>
    private string? RequiredVersion()
    {
        // PDF/A-1 files use none of these features.
        if (_report.PdfAPart == PdfA1)
        {
            return null;
        }

        // The JPXDecode filter and object streams both need PDF 1.5; JBIG2Decode needs PDF 1.4.
        if (_options.UseObjectStreams || (_options.OptimizeImages && _options.ImageCodec == PdfOptimizeImageCodec.Jpeg2000))
        {
            return ObjectStreamVersion;
        }

        return _options.Jbig2 != PdfJbig2Mode.Off ? DefaultVersion : null;
    }

    /// <summary>Lists the images to re-encode and the images used as masks.</summary>
    /// <param name="scanner">What the analysis learned.</param>
    /// <param name="graph">The reachable objects.</param>
    private void PlanImages(ContentUsageScanner scanner, OptimizerGraph graph)
    {
        if (!_options.OptimizeImages)
        {
            return;
        }

        for (var number = 1; number <= graph.Count; number++)
        {
            if (graph.GetValue(number).AsStream() is not { } stream || !stream.Dictionary.GetName(KnownName.Subtype).Is(KnownName.Image))
            {
                continue;
            }

            var old = graph.GetOldNumber(number);
            _images[old] = scanner.Images.GetValueOrDefault(old, ImageUse.Unseen);
            AddMask(stream.Dictionary.GetRaw(KnownName.SMask));
            AddMask(stream.Dictionary.GetRaw(KnownName.Mask));
        }
    }

    /// <summary>
    /// Folds each duplicate's plan into the object that survives it, as one object now serves every use: image uses
    /// combine (the largest use, fixed when any is), a mask stays a mask, and a font program keeps the glyphs of every
    /// copy, or stays whole when any copy must.
    /// </summary>
    /// <param name="aliases">The original of each duplicate, by old number.</param>
    private void MergeAliasedPlans(int[] aliases)
    {
        for (var number = 1; number < aliases.Length; number++)
        {
            var target = aliases[number];
            if (target <= 0)
            {
                continue;
            }

            MergeImage(number, target);
            MergeFont(number, target);
        }
    }

    /// <summary>Combines a duplicate image's uses into its original's.</summary>
    /// <param name="number">The duplicate.</param>
    /// <param name="target">The original.</param>
    private void MergeImage(int number, int target)
    {
        if (_masks.Contains(number))
        {
            _ = _masks.Add(target);
        }

        if (!_images.TryGetValue(number, out var use) || !_images.TryGetValue(target, out var kept))
        {
            return;
        }

        _images[target] = new(Math.Min(use.MinPpi, kept.MinPpi), use.Uses + kept.Uses, use.IsFixed || kept.IsFixed);
    }

    /// <summary>Combines a duplicate font program's glyphs into its original's, or keeps the original whole.</summary>
    /// <param name="number">The duplicate.</param>
    /// <param name="target">The original.</param>
    private void MergeFont(int number, int target)
    {
        if (_fonts is not { } fonts)
        {
            return;
        }

        var hasDuplicate = fonts.Kept.TryGetValue(number, out var glyphs);
        if (!fonts.Kept.TryGetValue(target, out var kept))
        {
            return;
        }

        if (hasDuplicate)
        {
            kept.UnionWith(glyphs!);
            return;
        }

        // The duplicate's fonts were not planned, so the shared program must keep every glyph.
        if (fonts.Kept.Remove(target, out var dropped))
        {
            fonts.Release(dropped);
        }
    }

    /// <summary>Records an image used as a mask.</summary>
    /// <param name="raw">The /SMask or /Mask value.</param>
    private void AddMask(PdfValue raw)
    {
        if (raw.IsReference)
        {
            _ = _masks.Add(raw.AsReference().Number);
        }
    }

    /// <summary>Counts the objects no page or catalog entry reaches, which the rewrite drops.</summary>
    /// <param name="graph">The reachable objects.</param>
    private void CountUnused(OptimizerGraph graph)
    {
        var store = Working.Objects;
        for (var number = 1; number < store.Size; number++)
        {
            if (graph.IsReached(number))
            {
                continue;
            }

            var value = StoreReading.GetObject(store, new(number, 0));
            if (!value.IsNull && !IsStructural(value))
            {
                _report.Measure(PdfOptimizeCategory.UnusedObjects, value.AsStream()?.RawLength ?? 0, 0);
            }
        }
    }

    /// <summary>Gives back everything the run leased and forgets the run's document and results.</summary>
    private void ReleaseRun()
    {
        _plan?.Dispose();
        _pendingGraph?.Return();
        _pendingGraph = null;
        _duplicates?.Return();
        _scanner?.Return();
        _plan = null;
        _duplicates = null;
        _scanner = null;
        _fonts?.Dispose();
        _fonts = null;
        _images.Clear();
        _masks.Clear();
        _report.Reset();
        _recoder.Clear();
        _working = null;
        _source = null!;
        _options = default;
        _progress = null;
        _cancellationToken = default;
    }

    /// <summary>Reports progress.</summary>
    /// <param name="phase">The step.</param>
    /// <param name="completed">The items done.</param>
    /// <param name="total">The items in the step.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Report(PdfOptimizePhase phase, int completed, int total) => _progress?.Report(new(phase, completed, total, 0));
}
