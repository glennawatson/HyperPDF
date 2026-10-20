// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Tests;

/// <summary>An editor that only saves, writing fixed bytes and noting the thread, so the save path can be tested without an engine.</summary>
internal sealed class FakeAnnotationEditor : IAnnotationEditor
{
    /// <summary>The bytes written when saving.</summary>
    private static readonly byte[] Output = "%PDF-1.7 fake saved file"u8.ToArray();

    /// <inheritdoc/>
    public IDocument Document => throw new NotSupportedException("The fake belongs to no document.");

    /// <inheritdoc/>
    public bool HasUnsavedChanges => true;

    /// <inheritdoc/>
    public string Author { get; set; } = string.Empty;

    /// <summary>Gets the managed thread the save started on.</summary>
    internal int ThreadId { get; private set; }

    /// <inheritdoc/>
    public void GetAnnotations(int pageIndex, List<PageAnnotation> output) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddMarkup(int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddInk(int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddNote(int pageIndex, PagePoint location, string contents, uint color) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddCaret(int pageIndex, PagePoint location, uint color) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddText(int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddShape(int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddStamp(int pageIndex, PagePoint location, string label, uint color) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddImageStamp(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddPolygon(int pageIndex, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddCallout(int pageIndex, PagePoint target, PagePoint location, string text, float fontSize, uint color) => throw new NotSupportedException();

    /// <inheritdoc/>
    public void GetReplies(int pageIndex, int index, List<AnnotationReply> output) => throw new NotSupportedException();

    /// <inheritdoc/>
    public int AddReply(int pageIndex, int index, string contents, ReviewState state) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool SetColor(int pageIndex, int index, uint color) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool SetContents(int pageIndex, int index, string contents) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool SetBounds(int pageIndex, int index, PageRect bounds) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool SetLineWidth(int pageIndex, int index, float width) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool SetFontSize(int pageIndex, int index, float fontSize) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool SetRemoved(int pageIndex, int index, bool removed) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool Remove(int pageIndex, int index) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool Save(Stream destination) => throw new NotSupportedException();

    /// <inheritdoc/>
    public async ValueTask<bool> SaveAsync(Stream destination, CancellationToken cancellationToken)
    {
        ThreadId = Environment.CurrentManagedThreadId;
        await destination.WriteAsync(Output, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
