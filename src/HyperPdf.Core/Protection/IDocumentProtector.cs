// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Protection;

/// <summary>Writes copies of the open document with passwords and permissions, or without protection.</summary>
public interface IDocumentProtector : IDocumentFeature
{
    /// <summary>Describes how the document is protected now.</summary>
    /// <returns>The status.</returns>
    ProtectionStatus GetStatus();

    /// <summary>
    /// Writes a protected copy, or explains why it cannot. The open document is never changed, and nothing is written to
    /// <paramref name="destination"/> when the copy is refused.
    /// </summary>
    /// <param name="request">The passwords and permissions.</param>
    /// <param name="destination">The writable stream that receives the copy.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    Task<ProtectionResult> ProtectAsync(ProtectionRequest request, Stream destination, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a copy without protection, or explains why it cannot. The open document is never changed, and nothing is
    /// written to <paramref name="destination"/> when the copy is refused.
    /// </summary>
    /// <param name="ownerPassword">The owner password, needed when the document was opened with its user password; or <see langword="null"/>.</param>
    /// <param name="destination">The writable stream that receives the copy.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    Task<ProtectionResult> RemoveProtectionAsync(string? ownerPassword, Stream destination, CancellationToken cancellationToken);
}
