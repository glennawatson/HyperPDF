// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Exporting;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Security;
using HyperPdf.ViewModels.Signing;
using HyperPdf.Viewer.Icons;
using ReactiveUI;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// The windows that write a changed copy of the document: a plain copy, a smaller one, an archive copy, a PDF 2.0 copy, a flattened
/// one, pages as pictures, a protected or unprotected copy, a redacted one and one with signature validation data. Each is a table
/// of rows over its view model, with its primary save action in the footer.
/// </summary>
internal static class CopyForms
{
    /// <summary>The largest resolution pictures of pages are written at, in dots per inch.</summary>
    private const int MaxDpi = 1200;

    /// <summary>The smallest resolution pictures of pages are written at, in dots per inch.</summary>
    private const int MinDpi = 36;

    /// <summary>The English label of the primary button that saves a copy.</summary>
    private const string SaveCopyText = "Save the copy…";

    /// <summary>The rows of the save a copy window.</summary>
    private static readonly FormSetting<SaveCopyViewModel>[] SaveCopyRows =
    [
        FormSettings.Pick<SaveCopyViewModel, SaveCopyFormatChoice>(
            "Kind of copy",
            static m => m.Formats,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedFormat,
            static (m, c) => m.SelectedFormat = c),
        FormSettings.Text<SaveCopyViewModel>(static m => m.SelectedFormat is { } format ? UiText.Get(format.DescriptionKey) : null),
        FormSettings.Pick<SaveCopyViewModel, OptimizePresetChoice>(
            "Size",
            static m => m.Optimize.Presets,
            static c => UiText.Get(c.NameKey),
            static m => m.Optimize.SelectedPreset,
            static (m, c) => m.Optimize.SelectedPreset = c).When(static m => m.IsOptimizeFormat),
        FormSettings.Toggle<SaveCopyViewModel>(
            "Fix accessibility problems",
            static m => m.Optimize.FixAccessibility,
            static (m, on) => m.Optimize.FixAccessibility = on).When(static m => m.IsOptimizeFormat),
        FormSettings.Pick<SaveCopyViewModel, ArchiveLevelChoice>(
            "Archive standard",
            static m => m.Archive.Levels,
            static c => UiText.Get(c.NameKey),
            static m => m.Archive.SelectedLevel,
            static (m, c) => m.Archive.SelectedLevel = c).When(static m => m.IsArchiveFormat),
        FormSettings.Pick<SaveCopyViewModel, FlattenScopeChoice>(
            "What to flatten",
            static m => m.Flatten.Scopes,
            static c => UiText.Get(c.NameKey),
            static m => m.Flatten.SelectedScope,
            static (m, c) => m.Flatten.SelectedScope = c).When(static m => m.IsFlattenFormat),
        FormSettings.Action<SaveCopyViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<SaveCopyViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<SaveCopyViewModel>(static m => m.Error),
        FormSettings.Text<SaveCopyViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the smaller copy window.</summary>
    private static readonly FormSetting<OptimizeCopyViewModel>[] OptimizeRows =
    [
        FormSettings.Pick<OptimizeCopyViewModel, OptimizePresetChoice>(
            "Size",
            static m => m.Presets,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedPreset,
            static (m, c) => m.SelectedPreset = c),
        FormSettings.Text<OptimizeCopyViewModel>(static m => m.SelectedPreset is { } preset ? UiText.Get(preset.DescriptionKey) : null),
        FormSettings.Toggle<OptimizeCopyViewModel>("Fix accessibility problems", static m => m.FixAccessibility, static (m, on) => m.FixAccessibility = on),
        FormSettings.Field<OptimizeCopyViewModel>("Document language, such as en-AU", static m => m.Language, static (m, text) => m.Language = text),
        FormSettings.Toggle<OptimizeCopyViewModel>("Add tags worked out from the layout", static m => m.AddInferredTags, static (m, on) => m.AddInferredTags = on),
        FormSettings.Toggle<OptimizeCopyViewModel>("Describe pictures", static m => m.DescribePictures, static (m, on) => m.DescribePictures = on).When(static m => m.CanDescribePictures),
        FormSettings.Text<OptimizeCopyViewModel>(static m => m.PictureDescriptionNote),
        FormSettings.Toggle<OptimizeCopyViewModel>("Remove unused parts", static m => m.CleanUp, static (m, on) => m.CleanUp = on),
        FormSettings.Action<OptimizeCopyViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<OptimizeCopyViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<OptimizeCopyViewModel>(static m => m.Error),
        FormSettings.Text<OptimizeCopyViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the archive copy window.</summary>
    private static readonly FormSetting<ArchiveCopyViewModel>[] ArchiveRows =
    [
        FormSettings.Pick<ArchiveCopyViewModel, ArchiveLevelChoice>(
            "Archive standard",
            static m => m.Levels,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedLevel,
            static (m, c) => m.SelectedLevel = c),
        FormSettings.Text<ArchiveCopyViewModel>(static m => m.SelectedLevel is { } level ? UiText.Get(level.DescriptionKey) : null),
        FormSettings.Action<ArchiveCopyViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<ArchiveCopyViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<ArchiveCopyViewModel>(static m => m.Error),
        FormSettings.Text<ArchiveCopyViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the PDF 2.0 copy window.</summary>
    private static readonly FormSetting<Pdf20CopyViewModel>[] Pdf20Rows =
    [
        FormSettings.Note<Pdf20CopyViewModel>("Writes a copy in the PDF 2.0 format. The document shown is not changed."),
        FormSettings.Action<Pdf20CopyViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<Pdf20CopyViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<Pdf20CopyViewModel>(static m => m.Error),
        FormSettings.Text<Pdf20CopyViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the flattened copy window.</summary>
    private static readonly FormSetting<FlattenCopyViewModel>[] FlattenRows =
    [
        FormSettings.Pick<FlattenCopyViewModel, FlattenScopeChoice>(
            "What to flatten",
            static m => m.Scopes,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedScope,
            static (m, c) => m.SelectedScope = c),
        FormSettings.Text<FlattenCopyViewModel>(static m => m.SelectedScope is { } scope ? UiText.Get(scope.DescriptionKey) : null),
        FormSettings.Pick<FlattenCopyViewModel, FlattenLookChoice>(
            "Which look to keep",
            static m => m.Looks,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedLook,
            static (m, c) => m.SelectedLook = c),
        FormSettings.Toggle<FlattenCopyViewModel>("Turn pages upright", static m => m.TurnPagesUpright, static (m, on) => m.TurnPagesUpright = on),
        FormSettings.Action<FlattenCopyViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<FlattenCopyViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<FlattenCopyViewModel>(static m => m.Error),
        FormSettings.Text<FlattenCopyViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the window that writes pages as pictures or text.</summary>
    private static readonly FormSetting<ExportPagesViewModel>[] ExportRows =
    [
        FormSettings.Pick<ExportPagesViewModel, PageFileFormatInfo>(
            "File format",
            static m => m.Formats,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedFormat,
            static (m, c) => m.SelectedFormat = c),
        FormSettings.Text<ExportPagesViewModel>(static m => UiText.Get(m.SelectedFormat.DescriptionKey)),
        FormSettings.Pick<ExportPagesViewModel, ExportPagesScopeChoice>(
            "Pages",
            static m => m.Scopes,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedScope,
            static (m, c) => m.SelectedScope = c),
        FormSettings.Field<ExportPagesViewModel>("Pages to write, such as 1-3, 7", static m => m.PagesText, static (m, text) => m.PagesText = text),
        FormSettings.Field<ExportPagesViewModel>(
            "Dots per inch",
            static m => m.Dpi.ToString(CultureInfo.CurrentCulture),
            static (m, text) => m.Dpi = ReadDpi(text, m.Dpi)).When(static m => m.SelectedFormat.IsRaster),
        FormSettings.Toggle<ExportPagesViewModel>("Include notes and marks", static m => m.IncludeMarkup, static (m, on) => m.IncludeMarkup = on),
        FormSettings.Action<ExportPagesViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<ExportPagesViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<ExportPagesViewModel>(static m => m.Error),
        FormSettings.Text<ExportPagesViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the window that writes a protected copy.</summary>
    private static readonly FormSetting<ProtectViewModel>[] ProtectRows =
    [
        FormSettings.Pick<ProtectViewModel, ProtectionFormatChoice>(
            "Protection",
            static m => m.Formats,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedFormat,
            static (m, c) => m.SelectedFormat = c),
        FormSettings.Text<ProtectViewModel>(static m => m.SelectedFormat is { } format ? UiText.Get(format.DescriptionKey) : null),
        FormSettings.Secret<ProtectViewModel>(
            "The owner password this document has now",
            static m => m.CurrentOwnerPassword,
            static (m, text) => m.CurrentOwnerPassword = text)
            .When(static m => m.NeedsCurrentOwner),
        FormSettings.Secret<ProtectViewModel>("Password to open the copy", static m => m.UserPassword, static (m, text) => m.UserPassword = text),
        FormSettings.Secret<ProtectViewModel>("The same password again", static m => m.UserPasswordRepeat, static (m, text) => m.UserPasswordRepeat = text),
        FormSettings.Secret<ProtectViewModel>("Password to change the copy", static m => m.OwnerPassword, static (m, text) => m.OwnerPassword = text),
        FormSettings.Secret<ProtectViewModel>("The same password again", static m => m.OwnerPasswordRepeat, static (m, text) => m.OwnerPasswordRepeat = text),
        FormSettings.Heading<ProtectViewModel>("What readers may do"),
        FormSettings.Toggle<ProtectViewModel>("Print", static m => m.AllowPrint, static (m, on) => m.AllowPrint = on),
        FormSettings.Toggle<ProtectViewModel>("Copy text and pictures", static m => m.AllowCopy, static (m, on) => m.AllowCopy = on),
        FormSettings.Toggle<ProtectViewModel>("Change the document", static m => m.AllowEdit, static (m, on) => m.AllowEdit = on),
        FormSettings.Toggle<ProtectViewModel>("Fill in forms", static m => m.AllowForms, static (m, on) => m.AllowForms = on),
        FormSettings.Toggle<ProtectViewModel>("Read with a screen reader", static m => m.AllowAccessibility, static (m, on) => m.AllowAccessibility = on),
        FormSettings.Text<ProtectViewModel>(static m => m.AccessibilityWarning),
        FormSettings.Toggle<ProtectViewModel>("Protect the document's details too", static m => m.EncryptMetadata, static (m, on) => m.EncryptMetadata = on),
        FormSettings.Action<ProtectViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<ProtectViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<ProtectViewModel>(static m => m.Error),
        FormSettings.Text<ProtectViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the window that writes a copy without protection.</summary>
    private static readonly FormSetting<RemoveProtectionViewModel>[] UnprotectRows =
    [
        FormSettings.Note<RemoveProtectionViewModel>("Writes a copy anyone can open and change. The document shown is not changed."),
        FormSettings.Secret<RemoveProtectionViewModel>(
            "The owner password",
            static m => m.OwnerPassword,
            static (m, text) => m.OwnerPassword = text).When(static m => m.NeedsOwnerPassword),
        FormSettings.Action<RemoveProtectionViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<RemoveProtectionViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<RemoveProtectionViewModel>(static m => m.Error),
        FormSettings.Text<RemoveProtectionViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the redaction window.</summary>
    private static readonly FormSetting<RedactionViewModel>[] RedactRows =
    [
        FormSettings.Text<RedactionViewModel>(static m => m.Summary),
        FormSettings.Text<RedactionViewModel>(static m => m.SourceNote),
        FormSettings.Pick<RedactionViewModel, RedactionImageOption>(
            "Pictures under a marked area",
            static m => m.ImageOptions,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedImage,
            static (m, c) => m.SelectedImage = c),
        FormSettings.Pick<RedactionViewModel, RedactionLineArtOption>(
            "Drawings under a marked area",
            static m => m.LineArtOptions,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedLineArt,
            static (m, c) => m.SelectedLineArt = c),
        FormSettings.Toggle<RedactionViewModel>("Remove hidden text", static m => m.RemoveHiddenText, static (m, on) => m.RemoveHiddenText = on),
        FormSettings.Toggle<RedactionViewModel>("Remove links and comments", static m => m.RemoveLinksAndComments, static (m, on) => m.RemoveLinksAndComments = on),
        FormSettings.Toggle<RedactionViewModel>("Remove the document's details", static m => m.ScrubMetadata, static (m, on) => m.ScrubMetadata = on),
        FormSettings.Text<RedactionViewModel>(static m => m.WarningText),
        FormSettings.Toggle<RedactionViewModel>("I understand this cannot be undone in the copy", static m => m.Understands, static (m, on) => m.Understands = on),
        FormSettings.Action<RedactionViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<RedactionViewModel>(static m => m.StatusText),
        FormSettings.Text<RedactionViewModel>(static m => m.Error),
        FormSettings.Text<RedactionViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the window that adds signature validation data.</summary>
    private static readonly FormSetting<ValidationDataViewModel>[] ValidationRows =
    [
        FormSettings.Pick<ValidationDataViewModel, ValidationActionChoice>(
            "What to add",
            static m => m.Actions,
            static c => UiText.Get(c.NameKey),
            static m => m.SelectedAction,
            static (m, c) => m.SelectedAction = c),
        FormSettings.Text<ValidationDataViewModel>(static m => m.SelectedAction is { } action ? UiText.Get(action.DescriptionKey) : null),
        FormSettings.Toggle<ValidationDataViewModel>("Include the data of each signature", static m => m.IncludeVri, static (m, on) => m.IncludeVri = on),
        FormSettings.Action<ValidationDataViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<ValidationDataViewModel>(static m => Progress(m.IsRunning, m.ProgressPercent, m.StatusText)),
        FormSettings.Text<ValidationDataViewModel>(static m => m.Error),
        FormSettings.Text<ValidationDataViewModel>(static m => m.ResultText),
    ];

    /// <summary>Makes the save a copy window.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest SaveCopy(SaveCopyViewModel model) =>
        CopyWindow(UiText.Format("Save a copy of {0}", model.FileName), model, SaveCopyRows, SaveCopyText, ChromeIcon.Save, model.SaveCommand);

    /// <summary>Makes the smaller copy window.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Optimize(OptimizeCopyViewModel model) =>
        CopyWindow(UiText.Format("Save a smaller copy of {0}", model.FileName), model, OptimizeRows, SaveCopyText, ChromeIcon.Save, model.SaveCommand);

    /// <summary>Makes the archive copy window.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Archive(ArchiveCopyViewModel model) =>
        CopyWindow(UiText.Format("Save an archive copy of {0}", model.FileName), model, ArchiveRows, SaveCopyText, ChromeIcon.Save, model.SaveCommand);

    /// <summary>Makes the PDF 2.0 copy window.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Pdf20(Pdf20CopyViewModel model) =>
        CopyWindow(UiText.Format("Save a PDF 2.0 copy of {0}", model.FileName), model, Pdf20Rows, SaveCopyText, ChromeIcon.Save, model.SaveCommand);

    /// <summary>Makes the flattened copy window.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Flatten(FlattenCopyViewModel model) =>
        CopyWindow(UiText.Format("Save a flattened copy of {0}", model.FileName), model, FlattenRows, SaveCopyText, ChromeIcon.Save, model.SaveCommand);

    /// <summary>Makes the window that writes pages as pictures or text.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest ExportPages(ExportPagesViewModel model) =>
        CopyWindow(UiText.Format("Save pages of {0}", model.FileName), model, ExportRows, "Save the pages…", ChromeIcon.Save, model.SaveCommand);

    /// <summary>Makes the window that writes a protected copy.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Protect(ProtectViewModel model) =>
        CopyWindow(UiText.Format("Protect a copy of {0}", model.FileName), model, ProtectRows, SaveCopyText, ChromeIcon.Lock, model.SaveCommand);

    /// <summary>Makes the window that writes a copy without protection.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Unprotect(RemoveProtectionViewModel model) =>
        CopyWindow(UiText.Format("Remove protection from a copy of {0}", model.FileName), model, UnprotectRows, SaveCopyText, ChromeIcon.Unlock, model.SaveCommand);

    /// <summary>Makes the redaction window.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Redact(RedactionViewModel model) =>
        CopyWindow(UiText.Format("Redact a copy of {0}", model.FileName), model, RedactRows, "Save the redacted copy…", ChromeIcon.Save, model.ApplyCommand);

    /// <summary>Makes the window that adds signature validation data.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Validation(ValidationDataViewModel model) =>
        CopyWindow(UiText.Format("Keep signatures checkable in a copy of {0}", model.FileName), model, ValidationRows, SaveCopyText, ChromeIcon.Certificate, model.SaveCommand);

    /// <summary>Builds a copy window with its primary save command in the bottom bar.</summary>
    /// <typeparam name="T">The view model type.</typeparam>
    /// <param name="title">The window heading.</param>
    /// <param name="model">The view model.</param>
    /// <param name="rows">The form rows.</param>
    /// <param name="acceptKey">The English label of the primary button.</param>
    /// <param name="icon">The primary button icon.</param>
    /// <param name="command">The command run by the primary button.</param>
    /// <returns>The form request.</returns>
    private static FormRequest CopyWindow<T>(string title, T model, FormSetting<T>[] rows, string acceptKey, ChromeIcon icon, ICommand command)
        where T : class, IReactiveObject =>
        FormSettings.Window(title, model, rows) with
        {
            AcceptText = UiText.Get(acceptKey),
            AcceptIcon = icon,
            AcceptCommand = command,
            CloseText = UiText.Get("Close"),
        };

    /// <summary>Makes the line that tells how far the work is.</summary>
    /// <param name="running">Whether the work is going on.</param>
    /// <param name="percent">How much is done, out of 100.</param>
    /// <param name="status">What the work is doing.</param>
    /// <returns>The line.</returns>
    private static string Progress(bool running, double percent, string status) =>
        running ? string.Create(CultureInfo.CurrentCulture, $"{status} {percent:0}%") : status;

    /// <summary>Reads a resolution as it is typed, kept inside what a picture of a page can be written at.</summary>
    /// <param name="text">The typed text.</param>
    /// <param name="current">The resolution kept when the text is not a whole number.</param>
    /// <returns>The dots per inch.</returns>
    private static int ReadDpi(string text, int current) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.CurrentCulture, out var dpi) ? Math.Clamp(dpi, MinDpi, MaxDpi) : current;
}
