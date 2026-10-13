// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Attachments;

/// <summary>
/// Adds and removes embedded files. A file is named by the <see cref="DocumentAttachment.Index"/> that
/// <see cref="IAttachmentSource.GetAttachments"/> reports. Each edit is one step in the document's undo history, and a refusal
/// comes back as a result with the reason, for example when the document's PDF/A level does not allow attached files.
/// </summary>
public interface IAttachmentEditor : IDocumentFeature
{
    /// <summary>Attaches a file.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="data">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> AddAttachmentAsync(string name, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    /// <summary>Removes an attached file.</summary>
    /// <param name="index">The file's <see cref="DocumentAttachment.Index"/>.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> RemoveAttachmentAsync(int index, CancellationToken cancellationToken);
}
