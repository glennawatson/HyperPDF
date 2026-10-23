// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Attachments that pass every call to the real ones and record the thread of a save.</summary>
/// <param name="inner">The real attachments.</param>
/// <param name="document">The document the feature belongs to.</param>
/// <param name="engine">The engine that keeps the record.</param>
internal sealed class RecordingAttachments(IAttachmentSource inner, IDocument document, PausingEngine engine) : IAttachmentSource
{
    /// <inheritdoc/>
    public IDocument Document => document;

    /// <inheritdoc/>
    public IReadOnlyList<DocumentAttachment> GetAttachments() => inner.GetAttachments();

    /// <inheritdoc/>
    public bool SaveAttachment(int index, Stream destination)
    {
        engine.Calls.Enqueue(new(nameof(SaveAttachment), Environment.CurrentManagedThreadId));
        return inner.SaveAttachment(index, destination);
    }
}
