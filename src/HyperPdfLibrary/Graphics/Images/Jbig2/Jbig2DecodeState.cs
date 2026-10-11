// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Owns decoded segment results and borrows the page, global results and shared work budget.</summary>
[DebuggerDisplay("Jbig2Context: {_segments.Count} segments")]
internal sealed class Jbig2DecodeState : IDisposable
{
    /// <summary>The segments processed so far.</summary>
    private readonly List<Jbig2Segment> _segments = [];

    /// <summary>The page bitmap, or <see langword="null"/> for the global context.</summary>
    private readonly Jbig2Bitmap? _page;

    /// <summary>The global context, or <see langword="null"/>.</summary>
    private readonly Jbig2DecodeState? _globals;

    /// <summary>The work budget shared with the global context.</summary>
    private readonly Jbig2Workspace _workspace;

    /// <summary>Whether a page information segment has started the page.</summary>
    private bool _inPage;

    /// <summary>Initializes a new instance of the <see cref="Jbig2DecodeState"/> class.</summary>
    /// <param name="page">The page bitmap, or <see langword="null"/> for global segments.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <param name="globals">The global context, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal Jbig2DecodeState(Jbig2Bitmap? page, Jbig2Workspace workspace, Jbig2DecodeState? globals, CancellationToken cancellationToken = default)
    {
        _page = page;
        _workspace = workspace;
        _globals = globals;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the owned segment results.</summary>
    internal List<Jbig2Segment> Segments => _segments;

    /// <summary>Gets the borrowed page bitmap, or null for globals.</summary>
    internal Jbig2Bitmap? Page => _page;

    /// <summary>Gets the borrowed global segment results.</summary>
    internal Jbig2DecodeState? Globals => _globals;

    /// <summary>Gets the shared work budget and scratch bitmaps.</summary>
    internal Jbig2Workspace Workspace => _workspace;

    /// <summary>Gets the token for this page or global segment decode.</summary>
    internal CancellationToken CancellationToken { get; }

    /// <summary>Gets a reference to the flag indicating page information has started the page.</summary>
    internal ref bool InPage => ref _inPage;

    /// <summary>Gets or sets a value indicating whether a page information segment was processed.</summary>
    internal bool PageSeen { get; set; }

    /// <summary>Returns the buffers of every segment's results.</summary>
    public void Dispose()
    {
        foreach (var segment in _segments)
        {
            segment.Dispose();
        }

        _segments.Clear();
    }
}
