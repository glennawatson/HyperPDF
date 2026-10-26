// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Metadata;
using CP.Localisation.Avalonia;

namespace HyperPdf.App.Views;

/// <summary>Refreshes help composed from several translated resource keys when the interface culture changes.</summary>
/// <param name="name">The composed explanation to display.</param>
[DebuggerDisplay("LiveHelpExtension: {Name}")]
public sealed class LiveHelpExtension(string name)
{
    /// <summary>Gets the composed explanation's name.</summary>
    [ConstructorArgument("name")]
    public string Name { get; } = name;

    /// <summary>Creates a reflection-free observable help binding.</summary>
    /// <param name="serviceProvider">The XAML service provider.</param>
    /// <returns>The binding that follows interface culture changes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object ProvideValue(IServiceProvider serviceProvider) => LocalisationBinding.Create(UiText, this, static (state, _) => state.Resolve());

    /// <summary>Resolves each explicitly supported composition without inspecting members.</summary>
    /// <returns>The current translated explanation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The explanation name is not supported.</exception>
    private string Resolve() => Name switch
    {
        nameof(Descriptions.ProtectCopy) => Descriptions.ProtectCopy,
        nameof(Descriptions.FollowPageTimings) => Descriptions.FollowPageTimings,
        nameof(Descriptions.KeepKeyboardCursor) => Descriptions.KeepKeyboardCursor,
        nameof(Descriptions.SharpnessPreset) => Descriptions.SharpnessPreset,
        nameof(Descriptions.TextEdges) => Descriptions.TextEdges,
        nameof(Descriptions.StrokeAdjustment) => Descriptions.StrokeAdjustment,
        nameof(Descriptions.OwnerPasswordField) => Descriptions.OwnerPasswordField,
        nameof(Descriptions.OptimizeDescribePictures) => Descriptions.OptimizeDescribePictures,
        nameof(Descriptions.ExportPages) => Descriptions.ExportPages,
        nameof(Descriptions.ExportFormats) => Descriptions.ExportFormats,
        nameof(Descriptions.ExportScopes) => Descriptions.ExportScopes,
        nameof(Descriptions.ExportPageRange) => Descriptions.ExportPageRange,
        nameof(Descriptions.ExportResolution) => Descriptions.ExportResolution,
        nameof(Descriptions.ExportMarkup) => Descriptions.ExportMarkup,
        nameof(Descriptions.ExportProgress) => Descriptions.ExportProgress,
        nameof(Descriptions.ExportResult) => Descriptions.ExportResult,
        nameof(Descriptions.ExportCancel) => Descriptions.ExportCancel,
        nameof(Descriptions.ExportSave) => Descriptions.ExportSave,
        nameof(Descriptions.CloseWindow) => Descriptions.CloseWindow,
        _ => throw new ArgumentOutOfRangeException(nameof(Name), Name, "Unknown composed control help.")
    };
}
