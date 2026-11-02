// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Media;

/// <summary>
/// Makes the still picture and the fallback text for a 3D annotation. It decodes the U3D data with limits, draws one frame from the
/// annotation's initial view, and keeps the PDF's own data untouched, so saving the document keeps the 3D stream's bytes and view
/// settings. It never runs scripts or actions and never loads another file.
/// </summary>
public static class Pdf3DPresenter
{
    /// <summary>The subtype of a U3D stream.</summary>
    private const string U3dSubtype = "U3D";

    /// <summary>The activation condition for a model that starts when the page opens.</summary>
    private const string OpenCondition = "PO";

    /// <summary>The activation condition for a model that starts when the page becomes visible.</summary>
    private const string VisibleCondition = "PV";

    /// <summary>What the fallback text says when the annotation holds no U3D data.</summary>
    private const string Unreadable = "This 3D content is not U3D data that can be shown.";

    /// <summary>The grey of the picture's background.</summary>
    private const float DefaultBackground = 0.9F;

    /// <summary>Presents a 3D annotation.</summary>
    /// <param name="annotation">The annotation's 3D data.</param>
    /// <param name="description">The annotation's own description (<c>/Contents</c>), or <see langword="null"/>.</param>
    /// <param name="width">The picture's width in pixels.</param>
    /// <param name="height">The picture's height in pixels.</param>
    /// <param name="cancellationToken">Stops the decode and the drawing.</param>
    /// <returns>The picture and text to show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Pdf3DPresentation Present(Pdf3DAnnotation annotation, string? description, int width, int height, CancellationToken cancellationToken) =>
        Present(annotation, description, new(width, height, null, new(DefaultBackground, DefaultBackground, DefaultBackground, 1)), U3dLimits.Default, cancellationToken);

    /// <summary>Presents a 3D annotation.</summary>
    /// <param name="annotation">The annotation's 3D data.</param>
    /// <param name="description">The annotation's own description (<c>/Contents</c>), or <see langword="null"/>.</param>
    /// <param name="options">The size and background of the picture; its view name is ignored, as the annotation names its own view.</param>
    /// <param name="limits">What the decode may read.</param>
    /// <param name="cancellationToken">Stops the decode and the drawing.</param>
    /// <returns>The picture and text to show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/>, <paramref name="options"/> or <paramref name="limits"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Pdf3DPresentation Present(Pdf3DAnnotation annotation, string? description, U3dRenderOptions options, U3dLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(limits);
        var note = ActivationNote(annotation.Activation);
        if (annotation.Stream is not { Subtype: U3dSubtype } stream || stream.Data.RawLength > limits.MaxBytes)
        {
            return new(null, null, Describe(description, null), note);
        }

        var model = U3dModelReader.Read(stream.Data.DecodeToArray(cancellationToken), limits, cancellationToken);
        var frame = Draw(model, options with { ViewName = ViewNameOf(annotation, stream) }, cancellationToken);
        return new(model, frame, Describe(description, model), note);
    }

    /// <summary>Draws the model from a view, when it has anything to draw and the size is one a frame can have.</summary>
    /// <param name="model">The model.</param>
    /// <param name="options">The size, view and background.</param>
    /// <param name="cancellationToken">Stops the drawing.</param>
    /// <returns>The frame, or <see langword="null"/>.</returns>
    private static U3dFrame? Draw(U3dModel model, U3dRenderOptions options, CancellationToken cancellationToken) =>
        model.Meshes.Length > 0 && options.IsValid ? U3dRenderer.Render(model, options, cancellationToken) : null;

    /// <summary>Finds the name of the view node the annotation starts in: its initial view, or the stream's default view.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="stream">The 3D stream.</param>
    /// <returns>The U3D view node name, or <see langword="null"/> for the model's first view.</returns>
    private static string? ViewNameOf(Pdf3DAnnotation annotation, Pdf3DStream stream)
    {
        if (annotation.InitialView?.InternalName is { Length: > 0 } initial)
        {
            return initial;
        }

        var index = stream.DefaultViewIndex;
        return index is >= 0 && index < stream.Views.Length ? stream.Views[index.Value].InternalName : stream.DefaultViewSelector;
    }

    /// <summary>Says what the document's activation setting asks for.</summary>
    /// <param name="activation">The activation, or <see langword="null"/>.</param>
    /// <returns>The note, or <see langword="null"/> when the document asks for nothing.</returns>
    private static string? ActivationNote(Pdf3DActivation? activation) => activation?.ActivateOn switch
    {
        OpenCondition => "The document asks for the model to start when the page opens. It waits until you start it.",
        VisibleCondition => "The document asks for the model to start when the page is shown. It waits until you start it.",
        _ => null,
    };

    /// <summary>Joins the description, what the model holds and what could not be used into the fallback text.</summary>
    /// <param name="description">The annotation's own description.</param>
    /// <param name="model">The model, or <see langword="null"/>.</param>
    /// <returns>The text.</returns>
    private static string Describe(string? description, U3dModel? model)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description.Trim());
        }

        parts.Add(model is null ? Unreadable : Summarise(model));
        if (model is { Problems.Length: > 0 })
        {
            parts.Add($"Not shown: {string.Join(' ', model.Problems)}");
        }

        return string.Join(' ', parts);
    }

    /// <summary>Says what a model holds.</summary>
    /// <param name="model">The model.</param>
    /// <returns>A sentence with the counts of meshes, scene nodes and views.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Summarise(U3dModel model) =>
        string.Create(CultureInfo.InvariantCulture, $"3D model with {model.Meshes.Length} meshes, {model.Nodes.Length} scene nodes and {model.ViewNodes().Length} views.");
}
