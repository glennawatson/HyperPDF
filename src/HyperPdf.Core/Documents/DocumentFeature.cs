// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Finds one optional feature of a document, typed as its feature interface.</summary>
/// <typeparam name="TFeature">The feature interface.</typeparam>
public static class DocumentFeature<TFeature>
    where TFeature : class, IDocumentFeature
{
    /// <summary>Gets the feature when the document supports it.</summary>
    /// <param name="document">The document, or <see langword="null"/>.</param>
    /// <returns>The feature, or <see langword="null"/> for a null document or an unsupported feature.</returns>
    public static TFeature? Get(IDocument? document) => document?.GetFeature(typeof(TFeature)) as TFeature;

    /// <summary>Gets a feature the document must support, with the same null and failure behaviour as an explicit cast.</summary>
    /// <param name="document">The document, or <see langword="null"/>.</param>
    /// <returns>The feature, or <see langword="null"/> for a null document.</returns>
    /// <exception cref="InvalidCastException">The document does not support the feature.</exception>
    public static TFeature? Cast(IDocument? document) =>
        document is null ? null : DocumentFeature<TFeature>.Get(document) ?? throw new InvalidCastException("The document does not support the requested feature.");
}
