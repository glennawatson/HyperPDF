// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Preferences;
using HyperPdf.ViewModels.Services;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Describes the preferences window. Every change applies straight away and is kept for next time, as the view model does it.</summary>
internal static class PreferencesForm
{
    /// <summary>The rows of the window, top to bottom. A row's place in this list is the number its changes come back with.</summary>
    private static readonly PreferenceRow[] Rows =
    [
        PreferenceRow.Heading("Look"),
        PreferenceRow.Choice("Interface language", PreferencesViewModel.InterfaceLanguageOptions, static p => p.InterfaceLanguage, static (p, value) => p.InterfaceLanguage = value),
        PreferenceRow.Choice("Colour scheme", PreferencesViewModel.ColorSchemeOptions, static p => p.ColorScheme, static (p, value) => p.ColorScheme = value),
        PreferenceRow.Choice("Drawing (next start)", PreferencesViewModel.RenderBackendOptions, static p => p.RenderBackend, static (p, value) => p.RenderBackend = value),
        PreferenceRow.Choice("PDF page colour", PreferencesViewModel.PageToneOptions, static p => p.PageTone, static (p, value) => p.PageTone = value),
        PreferenceRow.Field("Paper, as six hex digits such as 000000", static p => p.CustomPaper, static (p, value) => p.CustomPaper = value).When(static p => p.UsesCustomPageTone),
        PreferenceRow.Field("Ink, as six hex digits such as FFFF00", static p => p.CustomInk, static (p, value) => p.CustomInk = value).When(static p => p.UsesCustomPageTone),
        PreferenceRow.Status(static p => p.CustomToneContrast).When(static p => p.UsesCustomPageTone),
        PreferenceRow.Choice("Text size", PreferencesViewModel.FontSizeOptions, static p => p.FontSize, static (p, value) => p.FontSize = value),
        PreferenceRow.Choice("Tool bar", PreferencesViewModel.ToolbarOptions, static p => p.Toolbar, static (p, value) => p.Toolbar = value),
        PreferenceRow.Choice("Motion", PreferencesViewModel.MotionOptions, static p => p.Motion, static (p, value) => p.Motion = value),
        PreferenceRow.Choice("Text cursor", PreferencesViewModel.CaretOptions, static p => p.Caret, static (p, value) => p.Caret = value),
        PreferenceRow.Heading("Opening documents"),
        PreferenceRow.Choice("Opening zoom", PreferencesViewModel.OpeningZoomOptions, static p => p.OpeningZoom, static (p, value) => p.OpeningZoom = value),
        PreferenceRow.Choice("When a file changes on disk", PreferencesViewModel.FileChangeOptions, static p => p.FileChange, static (p, value) => p.FileChange = value),
        PreferenceRow.Toggle("Reopen at the last page read", static p => p.ReopenAtLastPage, static (p, on) => p.ReopenAtLastPage = on),
        PreferenceRow.Toggle("Follow the document's opening action", static p => p.FollowOpenAction, static (p, on) => p.FollowOpenAction = on),
        PreferenceRow.Toggle("Follow the document's side panel choice", static p => p.FollowPageMode, static (p, on) => p.FollowPageMode = on),
        PreferenceRow.Toggle("Follow the document's page layout", static p => p.FollowPageLayout, static (p, on) => p.FollowPageLayout = on),
        PreferenceRow.Toggle("Show the document's title instead of its file name", static p => p.FollowDisplayDocTitle, static (p, on) => p.FollowDisplayDocTitle = on),
        PreferenceRow.Toggle("Follow the document's print settings", static p => p.FollowPrintDefaults, static (p, on) => p.FollowPrintDefaults = on),
        PreferenceRow.Heading("Reading and forms"),
        PreferenceRow.Toggle("Keep the keyboard cursor on the page", static p => p.KeepKeyboardCursor, static (p, on) => p.KeepKeyboardCursor = on),
        PreferenceRow.Toggle("Check spelling in forms and notes", static p => p.CheckSpelling, static (p, on) => p.CheckSpelling = on),
        PreferenceRow.Toggle("Ignore bookmark colours", static p => p.IgnoreOutlineColours, static (p, on) => p.IgnoreOutlineColours = on),
        PreferenceRow.Toggle("Play page transitions", static p => p.FollowPageTransitions, static (p, on) => p.FollowPageTransitions = on),
        PreferenceRow.Toggle("Turn pages on the document's timings", static p => p.FollowPageTimings, static (p, on) => p.FollowPageTimings = on),
        PreferenceRow.Toggle("List fonts that are only for preview", static p => p.ShowPreviewOnlyFonts, static (p, on) => p.ShowPreviewOnlyFonts = on),
        PreferenceRow.Choice("Form field highlight colour", PreferencesViewModel.FormHighlightColorOptions, static p => p.FormHighlightColor, static (p, value) => p.FormHighlightColor = value),
        PreferenceRow.Choice(
            "Form field highlight strength",
            PreferencesViewModel.FormHighlightOpacityOptions,
            static p => p.FormHighlightOpacity,
            static (p, value) => p.FormHighlightOpacity = value),
        PreferenceRow.Field("Name on comments", static p => p.CommentAuthor, static (p, value) => p.CommentAuthor = value),
        PreferenceRow.Heading("Sharpness of text and pictures"),
        PreferenceRow.Choice("Edges", SharpnessViewModel.PresetOptions, static p => p.Sharpness.Preset, static (p, value) => p.Sharpness.Preset = value),
        PreferenceRow.Choice("Text edges", SharpnessViewModel.TextEdgeOptions, static p => p.Sharpness.TextEdges, static (p, value) => p.Sharpness.TextEdges = value),
        PreferenceRow.Choice("Fit small text to pixels", SharpnessViewModel.TextHintingOptions, static p => p.Sharpness.TextHinting, static (p, value) => p.Sharpness.TextHinting = value),
        PreferenceRow.Choice("Small text contrast", SharpnessViewModel.TextContrastOptions, static p => p.Sharpness.TextContrast, static (p, value) => p.Sharpness.TextContrast = value),
        PreferenceRow.Choice("Fit lines to pixels", SharpnessViewModel.StrokeAdjustmentOptions, static p => p.Sharpness.StrokeAdjustment, static (p, value) => p.Sharpness.StrokeAdjustment = value),
        PreferenceRow.Choice("Thinnest line", SharpnessViewModel.MinimumLineOptions, static p => p.Sharpness.MinimumLineWidth, static (p, value) => p.Sharpness.MinimumLineWidth = value),
        PreferenceRow.Toggle("Smooth the edges of lines and shapes", static p => p.Sharpness.SmoothGraphics, static (p, on) => p.Sharpness.SmoothGraphics = on),
        PreferenceRow.Toggle("Smooth pictures", static p => p.Sharpness.SmoothImages, static (p, on) => p.Sharpness.SmoothImages = on),
        PreferenceRow.Toggle("Average pixels when shrinking big pictures", static p => p.Sharpness.AreaImageReduction, static (p, on) => p.Sharpness.AreaImageReduction = on),
        PreferenceRow.Toggle("Remember the shapes of small letters", static p => p.Sharpness.SmallTextMasks, static (p, on) => p.Sharpness.SmallTextMasks = on),
        PreferenceRow.Heading("Services"),
        PreferenceRow.Choice("Read aloud voice", PreferencesViewModel.SpeechEngineOptions, static p => p.SpeechEngine, static (p, value) => p.SpeechEngine = value),
        PreferenceRow.Field("Azure speech region", static p => p.AzureRegion, static (p, value) => p.AzureRegion = value).When(static p => p.UsesAzure),
        PreferenceRow.Secret("Azure speech key", static p => p.AzureKey, static (p, value) => p.AzureKey = value).When(static p => p.UsesAzure),
        PreferenceRow.Field("Timestamp server for signing", static p => p.TimestampServer, static (p, value) => p.TimestampServer = value),
    ];

    /// <summary>Makes the window for a preferences view model.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest For(PreferencesViewModel preferences) => new(
        UiText.Get("Preferences"),
        form => Describe(form, preferences),
        null,
        null,
        Signal.Merge(preferences.GetChangedObservable().AsSignal(), preferences.Sharpness.GetChangedObservable().AsSignal()),
        null,
        edits => edits.SubscribeSafe(edit => Apply(preferences, edit), ErrorTrace.Write));

    /// <summary>Adds the rows, reading each setting as it is now.</summary>
    /// <param name="form">Receives the rows.</param>
    /// <param name="preferences">The preferences.</param>
    private static void Describe(FormBuilder form, PreferencesViewModel preferences)
    {
        form.Text(UiText.Get("Changes apply straight away and are kept for next time."));
        for (var i = 0; i < Rows.Length; i++)
        {
            var row = Rows[i];
            if (row.Visible is { } visible && !visible(preferences))
            {
                continue;
            }

            var label = row.Label.Length > 0 ? UiText.Get(row.Label) : string.Empty;
            switch (row.Kind)
            {
                case FormRowKind.Text:
                    {
                        form.Text(row.Value(preferences));
                        break;
                    }

                case FormRowKind.Choice:
                    {
                        form.Choice(label, row.Options, row.Selected(preferences), i);
                        break;
                    }

                case FormRowKind.Toggle:
                    {
                        form.Toggle(label, row.Selected(preferences) != 0, i);
                        break;
                    }

                case FormRowKind.Field:
                    {
                        if (row.Masked)
                        {
                            form.Secret(label, row.Value(preferences), i);
                            break;
                        }

                        form.Field(label, row.Value(preferences), i);
                        break;
                    }

                default:
                    {
                        form.Heading(label);
                        break;
                    }
            }
        }
    }

    /// <summary>Writes a change the user made to the preferences.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <param name="edit">The change.</param>
    private static void Apply(PreferencesViewModel preferences, FormEdit edit)
    {
        if ((uint)edit.Id < (uint)Rows.Length)
        {
            Rows[edit.Id].Apply(preferences, edit);
        }
    }
}
