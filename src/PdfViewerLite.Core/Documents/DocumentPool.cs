// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Core.Documents;

/// <summary>
/// Limits how many native documents stay open. Tabs hold <see cref="DocumentSource"/> objects; once more than
/// <see cref="Capacity"/> are open the least recently used are closed, so hundreds of tabs cost little memory. Use from
/// the UI thread.
/// </summary>
[DebuggerDisplay("DocumentPool: {OpenCount} of {Capacity} open")]
public sealed class DocumentPool : IDisposable
{
    /// <summary>The default number of documents kept open.</summary>
    private const int DefaultCapacity = 16;

    /// <summary>Every live source.</summary>
    private readonly List<DocumentSource> _sources = [];

    /// <summary>The engine used to open documents.</summary>
    private readonly IDocumentEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="DocumentPool"/> class.</summary>
    /// <param name="engine">The document engine.</param>
    public DocumentPool(IDocumentEngine engine)
        : this(engine, DefaultCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DocumentPool"/> class.</summary>
    /// <param name="engine">The document engine.</param>
    /// <param name="capacity">The number of documents kept open.</param>
    public DocumentPool(IDocumentEngine engine, int capacity)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _engine = engine;
        Capacity = capacity;
    }

    /// <summary>Gets or sets the number of documents kept open.</summary>
    public int Capacity { get; set; }

    /// <summary>Gets the number of open documents.</summary>
    public int OpenCount
    {
        get
        {
            var count = 0;
            foreach (var source in _sources)
            {
                if (source.IsOpen)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Gets the engine.</summary>
    public IDocumentEngine Engine => _engine;

    /// <summary>Creates a source for a file. The file is not opened until <see cref="DocumentSource.Acquire"/>.</summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="password">The password.</param>
    /// <returns>The source.</returns>
    public DocumentSource Create(string filePath, string? password)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var source = new DocumentSource(this, filePath, password);
        _sources.Add(source);
        return source;
    }

    /// <summary>Closes a source and stops tracking it.</summary>
    /// <param name="source">The source.</param>
    public void Remove(DocumentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Close();
        _ = _sources.Remove(source);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var source in _sources)
        {
            source.Close();
        }

        _sources.Clear();
    }

    /// <summary>Opens a source and closes the least recently used ones beyond capacity.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The document.</returns>
    internal IDocument Acquire(DocumentSource source)
    {
        var document = source.OpenIfNeeded(_engine);
        Trim(source);
        return document;
    }

    /// <summary>Opens a source asynchronously, then applies the pool's capacity on its owning thread.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels a pending open.</param>
    /// <returns>The open document.</returns>
    /// <exception cref="OperationCanceledException">The open was cancelled.</exception>
    internal ValueTask<IDocument> AcquireAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return source.IsOpen ? new(Acquire(source)) : AcquireColdAsync(source, cancellationToken);
    }

    /// <summary>Awaits a cold open before applying the pool capacity.</summary>
    /// <param name="source">The source to open.</param>
    /// <param name="cancellationToken">Cancels a pending open.</param>
    /// <returns>The opened document.</returns>
    private async ValueTask<IDocument> AcquireColdAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        var document = await source.OpenIfNeededAsync(_engine, cancellationToken);
        Trim(source);
        return document;
    }

    /// <summary>Closes documents beyond capacity, never the one just used nor one with unsaved edits.</summary>
    /// <param name="keep">The source to keep open.</param>
    private void Trim(DocumentSource keep)
    {
        for (var open = OpenCount; open > Capacity; open--)
        {
            DocumentSource? oldest = null;
            foreach (var source in _sources)
            {
                if (source != keep && source.IsOpen && !source.HasUnsavedChanges && (oldest is null || source.LastUsed < oldest.LastUsed))
                {
                    oldest = source;
                }
            }

            if (oldest is null)
            {
                return;
            }

            oldest.Close();
        }
    }
}
