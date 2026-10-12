// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Tests.Fakes;

/// <summary>Opens <see cref="FakeDocument"/> instances and records them.</summary>
internal sealed class FakeEngine : IDocumentEngine
{
    /// <summary>The A4 page size in points.</summary>
    internal static readonly PageSize A4 = new(595, 842);

    /// <inheritdoc/>
    public string Name => "Fake";

    /// <summary>Gets every document opened.</summary>
    internal List<FakeDocument> Opened { get; } = [];

    /// <inheritdoc/>
    public bool CanOpen(string path) => true;

    /// <inheritdoc/>
    public IDocument Open(string path, string? password)
    {
        var document = new FakeDocument(path, A4, A4, A4);
        Opened.Add(document);
        return document;
    }
}
