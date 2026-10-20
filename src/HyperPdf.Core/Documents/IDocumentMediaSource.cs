// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Lists the sound and video a document carries and writes it out, so the reader can save it or open it with the system player. Nothing plays inside the viewer.</summary>
public interface IDocumentMediaSource : IDocumentFeature
{
    /// <summary>Gets the media of the document, in page order.</summary>
    /// <returns>The media; empty when the document has none.</returns>
    IReadOnlyList<DocumentMedia> GetMedia();

    /// <summary>Writes a piece of media into a stream, as a file the system player can open.</summary>
    /// <param name="index">The <see cref="DocumentMedia.Index"/>.</param>
    /// <param name="destination">The stream receiving the file.</param>
    /// <returns><see langword="true"/> when the whole file was written.</returns>
    bool SaveMedia(int index, Stream destination);
}
