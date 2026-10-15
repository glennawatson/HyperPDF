// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <content>Appearance streams and the reply and review entries.</content>
public static partial class PdfAnnotations
{
    /// <summary>Gets the Review state model's name.</summary>
    private static ReadOnlySpan<byte> ReviewModel => "Review"u8;

    /// <summary>Adds a Form XObject to the document and makes it the annotation's normal appearance.</summary>
    /// <param name="store">The document.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="form">The appearance, for example from <see cref="Writing.PdfContentBuilder.ToFormXObject(PdfObjectStore?, PdfRectangle, PdfDictionary?)"/>.</param>
    /// <returns>The appearance stream's object id.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfObjectId SetNormalAppearance(PdfObjectStore store, PdfDictionary annotation, PdfStream form)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(form);
        var id = StoreEditing.Add(store, PdfValue.FromStream(form), PdfCacheScopes.None);
        var appearances = new PdfDictionary(annotation.Owner, 1);
        appearances.Set(KnownName.N, PdfValue.FromReference(id));
        annotation.Set(KnownName.AP, PdfValue.FromDictionary(appearances));
        _ = annotation.Remove(KnownName.AS);
        return id;
    }

    /// <summary>Gets the normal appearance stream, following <c>/AS</c> when the appearance has states.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The stream, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static PdfStream? GetNormalAppearance(PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var normal = annotation.GetDictionary(KnownName.AP)?.Get(KnownName.N) ?? default;
        if (normal.AsStream() is { } stream)
        {
            return stream;
        }

        var state = annotation.GetName(KnownName.AS);
        return normal.AsDictionary() is { } states && !state.IsNone ? states.GetStream(state) : null;
    }

    /// <summary>Removes the appearance, so readers draw the annotation from its entries.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when there was one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static bool RemoveAppearance(PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return annotation.Remove(KnownName.AP);
    }

    /// <summary>Makes an annotation a reply to another: <c>/IRT</c> refers to it and <c>/RT /R</c> marks a reply.</summary>
    /// <param name="annotation">The reply.</param>
    /// <param name="parent">The annotation it answers, an indirect object.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetInReplyTo(PdfDictionary annotation, PdfObjectId parent)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        annotation.Set(KnownName.IRT, PdfValue.FromReference(parent));
        annotation.Set(KnownName.RT, PdfValue.FromName(KnownName.R));
    }

    /// <summary>Gets the annotation a reply answers.</summary>
    /// <param name="annotation">The reply.</param>
    /// <returns>The answered annotation when <c>/IRT</c> leads to an annotation dictionary, otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static PdfDictionary? GetInReplyTo(PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return annotation.GetDictionary(KnownName.IRT) is { } parent && parent.IsName(KnownName.Type, KnownName.Annot) ? parent : null;
    }

    /// <summary>Records a review state, such as <c>Accepted</c>, under the Review state model.</summary>
    /// <param name="annotation">The reply carrying the state.</param>
    /// <param name="state">The state's name, as ASCII.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetReviewState(PdfDictionary annotation, ReadOnlySpan<byte> state)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        annotation.Set(KnownName.State, PdfValue.FromString(state.ToArray()));
        annotation.Set(KnownName.StateModel, PdfValue.FromString(ReviewModel.ToArray()));
    }
}
