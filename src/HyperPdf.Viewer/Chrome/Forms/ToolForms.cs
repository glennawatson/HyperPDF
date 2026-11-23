// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Signatures;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Finding;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Reading;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Signing;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Icons;
using HyperPdf.Windowing.Rendering;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// The windows of the tools that ask for more than a file: the signatures of a document, signing with a certificate, signing with a
/// key held somewhere else, the mark of a hand signature, the look of typed text and searching a folder of documents.
/// </summary>
internal static class ToolForms
{
    /// <summary>The names of the ways a hand signature can be made, in the order of <see cref="SignatureMarkStyle"/>.</summary>
    private static readonly string[] MarkStyles = ["Typed in a handwriting font", "Drawn", "From a picture"];

    /// <summary>The rows of the signatures window.</summary>
    private static readonly FormSetting<SignaturesViewModel>[] SignatureRows =
    [
        FormSettings.Text<SignaturesViewModel>(static m => m.IsChecking ? UiText.Get("Checking the signatures…") : UiText.Format("Signatures in this document: {0}", m.SignatureCount)),
        FormSettings.List<SignaturesViewModel, DocumentSignature>(static m => [..m.Signatures], Describe, null),
        FormSettings.Action<SignaturesViewModel>("Check the signatures again", static m => m.CheckCommand).With(ChromeIcon.Certificate),
        FormSettings.Action<SignaturesViewModel>("Sign with a certificate…", static m => m.SignWithCertificateCommand).With(ChromeIcon.Certificate),
    ];

    /// <summary>The rows of the window that signs with a certificate.</summary>
    private static readonly FormSetting<CertificateSignViewModel>[] CertificateRows =
    [
        FormSettings.Choice<CertificateSignViewModel>(
            "What the signature allows afterwards",
            [],
            static m => m.CertificationIndex,
            static (m, value) => m.CertificationIndex = value) with { Options = static m => m.CertificationChoices },
        FormSettings.Toggle<CertificateSignViewModel>("Show the signature on the page", static m => m.IsVisibleSignature, static (m, on) => m.IsVisibleSignature = on),
        FormSettings.Pick<CertificateSignViewModel, RememberedCertificate>(
            "A certificate used before",
            static m => [..m.Remembered],
            static c => c.Subject,
            static m => m.SelectedCertificate,
            static (m, c) => m.SelectedCertificate = c).When(static m => m.HasRemembered),
        FormSettings.Action<CertificateSignViewModel>("Forget this certificate", static m => m.ForgetCommand).With(ChromeIcon.Close).When(static m => m.HasRemembered),
        FormSettings.Field<CertificateSignViewModel>("Certificate file", static m => m.CertificatePath, static (m, text) => m.CertificatePath = text),
        FormSettings.Action<CertificateSignViewModel>("Choose the certificate file…", static m => m.BrowseCommand).With(ChromeIcon.Open),
        FormSettings.Field<CertificateSignViewModel>("Password of the certificate", static m => Passwords.Mask(m.Password), static (m, text) => m.Password = Passwords.Typed(m.Password, text)),
        FormSettings.Toggle<CertificateSignViewModel>("Remember this certificate, without its password", static m => m.Remember, static (m, on) => m.Remember = on),
        FormSettings.Field<CertificateSignViewModel>("Reason for signing", static m => m.Reason, static (m, text) => m.Reason = text),
        FormSettings.Field<CertificateSignViewModel>("Where it is signed", static m => m.Location, static (m, text) => m.Location = text),
        FormSettings.Text<CertificateSignViewModel>(static m => m.PreviewText),
        FormSettings.Text<CertificateSignViewModel>(static m => m.Error),
        FormSettings.Action<CertificateSignViewModel>("Sign", static m => m.SignCommand).With(ChromeIcon.Signature),
    ];

    /// <summary>The rows of the window that signs with a key held somewhere else.</summary>
    private static readonly FormSetting<ExternalSignViewModel>[] ExternalRows =
    [
        FormSettings.Note<ExternalSignViewModel>("Signs with a key this app never sees. Save what must be signed, sign it with your own tool, then bring the result back."),
        FormSettings.Action<ExternalSignViewModel>("Choose the signer's certificate…", static m => m.BrowseCommand).With(ChromeIcon.Open),
        FormSettings.Text<ExternalSignViewModel>(static m => m.SignerText),
        FormSettings.Pick<ExternalSignViewModel, SigningKeyChoice>("Kind of key", static m => m.KeyChoices, static c => c.Name, static m => m.SelectedKey, static (m, c) => m.SelectedKey = c),
        FormSettings.Field<ExternalSignViewModel>("Reason for signing", static m => m.Reason, static (m, text) => m.Reason = text),
        FormSettings.Field<ExternalSignViewModel>("Where it is signed", static m => m.Location, static (m, text) => m.Location = text),
        FormSettings.Action<ExternalSignViewModel>("Get the document ready", static m => m.PrepareCommand).With(ChromeIcon.Done),
        FormSettings.Text<ExternalSignViewModel>(static m => m.PendingText),
        FormSettings.Action<ExternalSignViewModel>("Save what must be signed…", static m => m.SaveToSignCommand).With(ChromeIcon.Save).When(static m => m.HasPending),
        FormSettings.Pick<ExternalSignViewModel, ExternalResultChoice>(
            "What your tool gave back",
            static m => m.ResultChoices,
            static c => c.Name,
            static m => m.SelectedResult,
            static (m, c) => m.SelectedResult = c).When(static m => m.HasPending),
        FormSettings.Action<ExternalSignViewModel>("Bring the result back and save the signed copy…", static m => m.FinishCommand).With(ChromeIcon.Save).When(static m => m.HasPending),
        FormSettings.Action<ExternalSignViewModel>("Stop", static m => m.CancelCommand).With(ChromeIcon.Stop).When(static m => m.IsRunning),
        FormSettings.Text<ExternalSignViewModel>(static m => m.StatusText),
        FormSettings.Text<ExternalSignViewModel>(static m => m.Error),
        FormSettings.Text<ExternalSignViewModel>(static m => m.ResultText),
    ];

    /// <summary>The rows of the window that makes the mark of a hand signature or of initials.</summary>
    private static readonly FormSetting<SignatureMarkViewModel>[] MarkRows =
    [
        FormSettings.Options<SignatureMarkViewModel>("How to make it", [..UiText.CreateChoices(MarkStyles)], static m => (int)m.Style, static (m, value) => m.Style = (SignatureMarkStyle)value),
        FormSettings.Field<SignatureMarkViewModel>(static m => m.TextLabel, static m => m.Text, static (m, text) => m.Text = text).When(static m => m.Style == SignatureMarkStyle.Typed),
        FormSettings.Note<SignatureMarkViewModel>("Draw in the box on the right with the mouse, a pen or a finger.").When(static m => m.Style == SignatureMarkStyle.Drawn),
        FormSettings.Action<SignatureMarkViewModel>("Start the drawing again", static m => m.ClearDrawingCommand).With(ChromeIcon.Close).When(static m => m.Style == SignatureMarkStyle.Drawn),
        FormSettings.Action<SignatureMarkViewModel>("Choose a picture of it…", static m => m.ChooseImageCommand).With(ChromeIcon.Open).When(static m => m.Style == SignatureMarkStyle.Image),
        FormSettings.Text<SignatureMarkViewModel>(static m => m.SourceImage?.Name).When(static m => m.Style == SignatureMarkStyle.Image),
        FormSettings.Toggle<SignatureMarkViewModel>(
            "Take the paper out of the picture",
            static m => m.RemovePaper,
            static (m, on) => m.RemovePaper = on).When(static m => m.Style == SignatureMarkStyle.Image && m.SourceImage is not null),
        FormSettings.Text<SignatureMarkViewModel>(static m => m.ImageError).When(static m => m.Style == SignatureMarkStyle.Image),
        FormSettings.Toggle<SignatureMarkViewModel>("Keep it for next time", static m => m.Remember, static (m, on) => m.Remember = on),
        FormSettings.Action<SignatureMarkViewModel>("Forget the one that was kept", static m => m.ForgetCommand).With(ChromeIcon.Close).When(static m => m.HasSaved),
    ];

    /// <summary>The rows of the window that sets how typed text looks.</summary>
    private static readonly FormSetting<TextPropertiesViewModel>[] TextRows =
    [
        FormSettings.Field<TextPropertiesViewModel>("Text", static m => m.Text, static (m, text) => m.Text = text),
        FormSettings.Field<TextPropertiesViewModel>("Font", static m => m.FontFamily, static (m, text) => m.FontFamily = text),
        FormSettings.Field<TextPropertiesViewModel>("Size in points", static m => Number(m.FontSize), static (m, text) => m.FontSize = Number(text, m.FontSize)),
        FormSettings.Toggle<TextPropertiesViewModel>("Bold", static m => m.IsBold, static (m, on) => m.IsBold = on),
        FormSettings.Toggle<TextPropertiesViewModel>("Italic", static m => m.IsItalic, static (m, on) => m.IsItalic = on),
        FormSettings.Toggle<TextPropertiesViewModel>("Underlined", static m => m.IsUnderline, static (m, on) => m.IsUnderline = on),
        FormSettings.Choice<TextPropertiesViewModel>("Lines start at", TextPropertiesViewModel.Alignments, static m => m.AlignmentIndex, static (m, value) => m.AlignmentIndex = value),
        FormSettings.Choice<TextPropertiesViewModel>("Colour", TextPropertiesViewModel.ColorNames, static m => m.ColorIndex, static (m, value) => m.ColorIndex = value),
        FormSettings.Field<TextPropertiesViewModel>("Space between lines", static m => Number(m.LineSpacing), static (m, text) => m.LineSpacing = Number(text, m.LineSpacing)),
        FormSettings.Field<TextPropertiesViewModel>("Space between letters", static m => Number(m.CharacterSpacing), static (m, text) => m.CharacterSpacing = Number(text, m.CharacterSpacing)),
        FormSettings.Field<TextPropertiesViewModel>("Width the lines wrap at, 0 for none", static m => Number(m.WrapWidth), static (m, text) => m.WrapWidth = Number(text, m.WrapWidth)),
        FormSettings.Field<TextPropertiesViewModel>("Comb boxes, 0 for none", static m => Number(m.CombCells), static (m, text) => m.CombCells = Number(text, m.CombCells)),
        FormSettings.Action<TextPropertiesViewModel>("Use these", static m => m.AcceptCommand).With(ChromeIcon.Done),
    ];

    /// <summary>The rows of the window that adds a watermark, a header, a footer, page numbers, Bates numbers, a background or a crop. Each kind shows the rows it needs.</summary>
    private static readonly FormSetting<PageDecorationViewModel>[] DecorationRows =
    [
        FormSettings.Field<PageDecorationViewModel>("Words, with {page}, {pages}, {number} and {date}", static m => m.Text, static (m, text) => m.Text = text)
            .When(static m => m.IsText),
        FormSettings.Choice<PageDecorationViewModel>("Look of the letters", PageDecorationViewModel.Families, static m => m.FamilyIndex, static (m, value) => m.FamilyIndex = value)
            .When(static m => m.IsText),
        FormSettings.Toggle<PageDecorationViewModel>("Bold", static m => m.IsBold, static (m, on) => m.IsBold = on).When(static m => m.IsText),
        FormSettings.Toggle<PageDecorationViewModel>("Italic", static m => m.IsItalic, static (m, on) => m.IsItalic = on).When(static m => m.IsText),
        FormSettings.Field<PageDecorationViewModel>("Size in points", static m => Number(m.FontSize), static (m, text) => m.FontSize = Number(text, m.FontSize))
            .When(static m => m.IsText),
        FormSettings.Choice<PageDecorationViewModel>("Colour", PageDecorationViewModel.ColorNames, static m => m.ColorIndex, static (m, value) => m.ColorIndex = value)
            .When(static m => m.IsText),
        FormSettings.Choice<PageDecorationViewModel>("How solid", PageDecorationViewModel.OpacityNames, static m => m.OpacityIndex, static (m, value) => m.OpacityIndex = value)
            .When(static m => m.IsText),
        FormSettings.Choice<PageDecorationViewModel>("Where on the page", PageDecorationViewModel.AnchorNames, static m => m.AnchorIndex, static (m, value) => m.AnchorIndex = value)
            .When(static m => m.IsText),
        FormSettings.Field<PageDecorationViewModel>("Distance from the edge in points", static m => Number(m.Margin), static (m, text) => m.Margin = Number(text, m.Margin))
            .When(static m => m.IsText),
        FormSettings.Field<PageDecorationViewModel>("Turn anticlockwise by degrees", static m => Number(m.Rotation), static (m, text) => m.Rotation = Number(text, m.Rotation))
            .When(static m => m.IsWatermark),
        FormSettings.Toggle<PageDecorationViewModel>("Draw it behind the page's own content", static m => m.IsBehind, static (m, on) => m.IsBehind = on)
            .When(static m => m.IsText),
        FormSettings.Field<PageDecorationViewModel>(
            "Number the first page starts at",
            static m => m.FirstNumber.ToString(CultureInfo.CurrentCulture),
            static (m, text) => m.FirstNumber = WholeNumber(text, m.FirstNumber)).When(static m => m.IsBates),
        FormSettings.Field<PageDecorationViewModel>(
            "Digits in the number, with zeros in front",
            static m => m.Digits.ToString(CultureInfo.CurrentCulture),
            static (m, text) => m.Digits = WholeNumber(text, m.Digits)).When(static m => m.IsBates),
        FormSettings.Choice<PageDecorationViewModel>(
            "Background colour",
            PageDecorationViewModel.BackgroundNames,
            static m => m.BackgroundIndex,
            static (m, value) => m.BackgroundIndex = value).When(static m => m.IsBackground),
        FormSettings.Field<PageDecorationViewModel>("Take this much off the left, in points", static m => Number(m.CropLeft), static (m, text) => m.CropLeft = Number(text, m.CropLeft))
            .When(static m => m.IsCrop),
        FormSettings.Field<PageDecorationViewModel>("Take this much off the top, in points", static m => Number(m.CropTop), static (m, text) => m.CropTop = Number(text, m.CropTop))
            .When(static m => m.IsCrop),
        FormSettings.Field<PageDecorationViewModel>("Take this much off the right, in points", static m => Number(m.CropRight), static (m, text) => m.CropRight = Number(text, m.CropRight))
            .When(static m => m.IsCrop),
        FormSettings.Field<PageDecorationViewModel>("Take this much off the bottom, in points", static m => Number(m.CropBottom), static (m, text) => m.CropBottom = Number(text, m.CropBottom))
            .When(static m => m.IsCrop),
        FormSettings.Note<PageDecorationViewModel>("Leave every margin at 0 to show the whole page again.").When(static m => m.IsCrop),
        FormSettings.Choice<PageDecorationViewModel>("Pages", PageDecorationViewModel.ScopeNames, static m => m.ScopeIndex, static (m, value) => m.ScopeIndex = value),
        FormSettings.Field<PageDecorationViewModel>("Page numbers, such as 1-3, 7", static m => m.RangeText, static (m, text) => m.RangeText = text)
            .When(static m => m.AsksForPages),
        FormSettings.Action<PageDecorationViewModel>("Add it", static m => m.AcceptCommand).With(ChromeIcon.Add),
    ];

    /// <summary>The rows of the window that lists the pages whose words differ from another document.</summary>
    private static readonly FormSetting<CompareViewModel>[] CompareRows =
    [
        FormSettings.Status<CompareViewModel>(static m => m.Result),
        FormSettings.Picker<CompareViewModel, ComparedPage>(static m => m.Differences, CompareViewModel.Describe, static (m, page) => m.GoTo(page)),
    ];

    /// <summary>The rows of the window that takes hidden information out of the document.</summary>
    private static readonly FormSetting<SanitizeViewModel>[] SanitizeRows =
    [
        FormSettings.Note<SanitizeViewModel>("Take out what you do not want to share. Your open file stays as it is until you save; Undo is in the Pages menu."),
        FormSettings.Toggle<SanitizeViewModel>("Clear the title, author, subject, keywords and the program that made it", static m => m.Metadata, static (m, on) => m.Metadata = on),
        FormSettings.Toggle<SanitizeViewModel>("Remove scripts", static m => m.Scripts, static (m, on) => m.Scripts = on),
        FormSettings.Toggle<SanitizeViewModel>("Remove attached files", static m => m.Attachments, static (m, on) => m.Attachments = on),
        FormSettings.Toggle<SanitizeViewModel>("Remove comments and marks, but keep links and form fields", static m => m.Comments, static (m, on) => m.Comments = on),
        FormSettings.Action<SanitizeViewModel>("Remove them", static m => m.AcceptCommand).With(ChromeIcon.Close),
    ];

    /// <summary>The rows of the window that numbers a run of pages in a different way.</summary>
    private static readonly FormSetting<PageLabelsViewModel>[] LabelRows =
    [
        FormSettings.Field<PageLabelsViewModel>("From page", static m => m.FromPage, static (m, text) => m.FromPage = text),
        FormSettings.Field<PageLabelsViewModel>("To page, or leave empty for the end of the document", static m => m.ToPage, static (m, text) => m.ToPage = text),
        FormSettings.Choice<PageLabelsViewModel>("How the number is written", PageLabelsViewModel.StyleNames, static m => m.StyleIndex, static (m, value) => m.StyleIndex = value),
        FormSettings.Field<PageLabelsViewModel>("Words before the number, such as A-", static m => m.Prefix, static (m, text) => m.Prefix = text),
        FormSettings.Field<PageLabelsViewModel>("Number of the first page", static m => m.FirstNumber, static (m, text) => m.FirstNumber = text),
        FormSettings.Action<PageLabelsViewModel>("Number these pages", static m => m.AcceptCommand).With(ChromeIcon.Done),
        FormSettings.Action<PageLabelsViewModel>("Number every page 1, 2, 3 again", static m => m.ResetCommand).With(ChromeIcon.Undo),
    ];

    /// <summary>The rows of the window that changes the chosen tag of the document: what it is, what it says, where it sits and what is done with it.</summary>
    private static readonly FormSetting<TagsViewModel>[] TagRows =
    [
        FormSettings.Text<TagsViewModel>(static m => m.SelectedItem?.Row),
        TagChoice("Kind of tag", static m => m.TypeChoices, static m => m.SelectedType, static (m, kind) => m.SelectedType = kind),
        FormSettings.Field<TagsViewModel>("What it is, for those who cannot see it", static m => m.AlternateText, static (m, text) => m.AlternateText = text),
        FormSettings.Field<TagsViewModel>("The text it stands for", static m => m.ActualText, static (m, text) => m.ActualText = text),
        FormSettings.Field<TagsViewModel>("Language", static m => m.Language, static (m, text) => m.Language = text),
        FormSettings.Field<TagsViewModel>("Title", static m => m.Title, static (m, text) => m.Title = text),
        TagChoice("Where it sits", static m => m.PlacementChoices, static m => m.SelectedPlacement, static (m, place) => m.SelectedPlacement = place)
            .When(static m => m.CanChangePlacement),
        TagChoice("How the list is numbered", static m => m.ListNumberingChoices, static m => m.SelectedListNumbering, static (m, kind) => m.SelectedListNumbering = kind)
            .When(static m => m.IsList),
        TagChoice("What the header cell heads", static m => m.ScopeChoices, static m => m.SelectedScope, static (m, scope) => m.SelectedScope = scope)
            .When(static m => m.IsHeaderCell),
        FormSettings.Field<TagsViewModel>("Rows the cell spans", static m => m.RowSpanText, static (m, text) => m.RowSpanText = text).When(static m => m.IsCell),
        FormSettings.Field<TagsViewModel>("Columns the cell spans", static m => m.ColumnSpanText, static (m, text) => m.ColumnSpanText = text).When(static m => m.IsCell),
        FormSettings.Action<TagsViewModel>("Save the changes to this tag", static m => m.ApplyCommand).With(ChromeIcon.Done),
        FormSettings.Action<TagsViewModel>("Read this tag earlier", static m => m.MoveUpCommand).With(ChromeIcon.PageUp),
        FormSettings.Action<TagsViewModel>("Read this tag later", static m => m.MoveDownCommand).With(ChromeIcon.PageDown),
        FormSettings.Action<TagsViewModel>("Put it inside the tag above", static m => m.NestCommand),
        FormSettings.Action<TagsViewModel>("Take it out of its parent", static m => m.UnnestCommand),
        TagChoice("Move it into", static m => m.MoveTargets, MoveTargetOf, static (m, target) => m.MoveTargetIndex = IndexOf(m.MoveTargets, target)),
        FormSettings.Action<TagsViewModel>("Move it there", static m => m.MoveIntoCommand),
        FormSettings.Action<TagsViewModel>("Remove it", static m => m.RemoveCommand).With(ChromeIcon.Close),
        FormSettings.Action<TagsViewModel>("Undo the last change to the document", static m => m.UndoCommand).With(ChromeIcon.Undo),
        FormSettings.Heading<TagsViewModel>("Custom tag types"),
        TagChoice("Custom type", static m => m.RoleMappings, RoleMappingOf, static (m, mapping) => m.RoleMappingIndex = IndexOf(m.RoleMappings, mapping)),
        FormSettings.Action<TagsViewModel>("Remove the custom type", static m => m.RemoveRoleMappingCommand).With(ChromeIcon.Close),
        FormSettings.Field<TagsViewModel>("New custom type name", static m => m.NewRoleType, static (m, text) => m.NewRoleType = text),
        TagChoice("It stands for", static m => m.StandardTypes, static m => m.NewRoleTarget, static (m, target) => m.NewRoleTarget = target),
        FormSettings.Action<TagsViewModel>("Add the custom type", static m => m.AddRoleMappingCommand).With(ChromeIcon.Add),
        FormSettings.Heading<TagsViewModel>("What the check found"),
        FormSettings.Text<TagsViewModel>(static m => m.ProblemSummary),
        FormSettings.Picker<TagsViewModel, TagProblemItem>(static m => m.Problems, static problem => problem.Text, static (m, problem) => m.SelectedProblem = problem),
        FormSettings.Text<TagsViewModel>(static m => m.Status),
    ];

    /// <summary>The rows of the window that plays sound or film: what is playing, how far it is, and the buttons that play it.</summary>
    private static readonly FormSetting<MediaPlayerViewModel>[] MediaRows =
    [
        FormSettings.Text<MediaPlayerViewModel>(static m => m.Caption),
        FormSettings.Text<MediaPlayerViewModel>(static m => m.Clock),
        FormSettings.Text<MediaPlayerViewModel>(static m => m.Status),
        FormSettings.Action<MediaPlayerViewModel>("Play", static m => m.PlayCommand).With(ChromeIcon.Play),
        FormSettings.Action<MediaPlayerViewModel>("Pause", static m => m.PauseCommand).With(ChromeIcon.Pause),
        FormSettings.Action<MediaPlayerViewModel>("Stop", static m => m.StopCommand).With(ChromeIcon.Stop),
        FormSettings.Action<MediaPlayerViewModel>("Back ten seconds", static m => m.SkipBackCommand).With(ChromeIcon.Back),
        FormSettings.Action<MediaPlayerViewModel>("Forward ten seconds", static m => m.SkipForwardCommand).With(ChromeIcon.Forward),
    ];

    /// <summary>The rows of the window that searches a folder of documents.</summary>
    private static readonly FormSetting<FolderSearchViewModel>[] FolderRows =
    [
        FormSettings.Field<FolderSearchViewModel>("Folder", static m => m.Folder, static (m, text) => m.Folder = text),
        FormSettings.Action<FolderSearchViewModel>("Choose the folder…", static m => m.ChooseFolderCommand).With(ChromeIcon.Open),
        FormSettings.Field<FolderSearchViewModel>("Words to find", static m => m.Query, static (m, text) => m.Query = text),
        FormSettings.Toggle<FolderSearchViewModel>("Match capital letters", static m => m.MatchCase, static (m, on) => m.MatchCase = on),
        FormSettings.Toggle<FolderSearchViewModel>("Whole words only", static m => m.WholeWord, static (m, on) => m.WholeWord = on),
        FormSettings.Toggle<FolderSearchViewModel>("Treat accented letters as plain", static m => m.IgnoreDiacritics, static (m, on) => m.IgnoreDiacritics = on),
        FormSettings.Toggle<FolderSearchViewModel>("Treat look-alike characters as the same", static m => m.FoldCompatibility, static (m, on) => m.FoldCompatibility = on),
        FormSettings.Toggle<FolderSearchViewModel>("Look in folders inside it too", static m => m.IncludeSubfolders, static (m, on) => m.IncludeSubfolders = on),
        FormSettings.Action<FolderSearchViewModel>("Search", static m => m.SearchCommand).With(ChromeIcon.Search),
        FormSettings.Action<FolderSearchViewModel>("Stop", static m => m.StopCommand).With(ChromeIcon.Stop).When(static m => m.IsSearching),
        FormSettings.Text<FolderSearchViewModel>(static m => m.Status),
        FormSettings.List<FolderSearchViewModel, FolderSearchResultViewModel>(static m => [..m.Results], static r => r.SpokenText, static m => m.OpenCommand),
    ];

    /// <summary>The rows of the window that opens a document locked with a certificate.</summary>
    private static readonly FormSetting<DocumentTabViewModel>[] CertificateLockRows =
    [
        FormSettings.Note<DocumentTabViewModel>("This document is locked for the holders of a certificate. Choose your certificate file and type its password."),
        FormSettings.Field<DocumentTabViewModel>("Certificate file", static m => m.CertificatePathEntry, static (m, text) => m.CertificatePathEntry = text),
        FormSettings.Action<DocumentTabViewModel>("Choose the certificate file…", static m => m.BrowseCertificateCommand).With(ChromeIcon.Open),
        FormSettings.Field<DocumentTabViewModel>(
            "Password of the certificate",
            static m => Passwords.Mask(m.CertificatePasswordEntry),
            static (m, text) => m.CertificatePasswordEntry = Passwords.Typed(m.CertificatePasswordEntry, text)),
        FormSettings.Text<DocumentTabViewModel>(static m => m.CertificateError),
        FormSettings.Action<DocumentTabViewModel>("Open the document", static m => m.SubmitCertificateCommand).With(ChromeIcon.Done),
    ];

    /// <summary>The rows of the window of a note on the page: its words, the replies to it and what can be done with it.</summary>
    private static readonly FormSetting<AnnotationsViewModel>[] BubbleRows =
    [
        FormSettings.Field<AnnotationsViewModel>("The note", static m => m.BubbleNoteText, static (m, text) => m.BubbleNoteText = text),
        FormSettings.Action<AnnotationsViewModel>("Keep the note", static m => m.SaveBubbleCommand).With(ChromeIcon.Done),
        FormSettings.Action<AnnotationsViewModel>("Reply to the note", static m => m.StartBubbleReplyCommand).With(ChromeIcon.Note).When(static m => m.CanReplyInBubble),
        FormSettings.Field<AnnotationsViewModel>("Your reply", static m => m.BubbleReplyText, static (m, text) => m.BubbleReplyText = text).When(static m => m.IsReplyingInBubble),
        FormSettings.Action<AnnotationsViewModel>("Send the reply", static m => m.SendBubbleReplyCommand).With(ChromeIcon.Done).When(static m => m.IsReplyingInBubble),
        FormSettings.Action<AnnotationsViewModel>("Remove the note", static m => m.DeleteBubbleAnnotationCommand).With(ChromeIcon.Close).When(static m => m.IsExistingBubble),
    ];

    /// <summary>The rows of the window of Focus Mode's text settings, which apply as they are changed: the text, how it is spread, its colours, and what is marked while it is read aloud.</summary>
    private static readonly FormSetting<FocusModeViewModel>[] FocusRows =
    [
        FormSettings.Note<FocusModeViewModel>("Change the size, spacing, width, colour and typeface of the text. Each change shows at once."),
        FormSettings.Field<FocusModeViewModel>("Text size in points", static m => Number(m.FontSize), static (m, text) => m.FontSize = Number(text, m.FontSize)),
        FormSettings.Field<FocusModeViewModel>("Space between lines, in text sizes", static m => Number(m.LineSpacing), static (m, text) => m.LineSpacing = Number(text, m.LineSpacing)),
        FormSettings.Field<FocusModeViewModel>(
            "Space between paragraphs, in text sizes",
            static m => Number(m.ParagraphSpacing),
            static (m, text) => m.ParagraphSpacing = Number(text, m.ParagraphSpacing)),
        FormSettings.Field<FocusModeViewModel>("How wide the column of text is, in characters", static m => Number(m.TextWidth), static (m, text) => m.TextWidth = Number(text, m.TextWidth)),
        FormSettings.Choice<FocusModeViewModel>("Colour behind the text", FocusModeViewModel.PageColourOptions, static m => m.PageColour, static (m, value) => m.PageColour = value),
        FormSettings.Choice<FocusModeViewModel>("Typeface", FocusModeViewModel.FontOptions, static m => m.Font, static (m, value) => m.Font = value),
        FormSettings.Toggle<FocusModeViewModel>("Dim the text away from what is read aloud", static m => m.FocusBand, static (m, on) => m.FocusBand = on),
        FormSettings.Toggle<FocusModeViewModel>("Mark each word as it is read aloud", static m => m.WordHighlight, static (m, on) => m.WordHighlight = on),
    ];

    /// <summary>The rows of the window that filters and sorts the list of notes and marks, which changes as the choices do.</summary>
    private static readonly FormSetting<AnnotationsViewModel>[] NoteFilterRows =
    [
        FormSettings.Note<AnnotationsViewModel>("Show only some of the notes and marks. The list shows the change at once."),
        FormSettings.Field<AnnotationsViewModel>("Words the notes contain", static m => m.FilterText, static (m, text) => m.FilterText = text),
        FormSettings.Choice<AnnotationsViewModel>("Type", AnnotationsViewModel.TypeFilterOptions, static m => m.FilterType, static (m, value) => m.FilterType = value),
        FormSettings.Choice<AnnotationsViewModel>("Colour", AnnotationsViewModel.ColorFilterOptions, static m => m.FilterColor, static (m, value) => m.FilterColor = value),
        FormSettings.Pick<AnnotationsViewModel, string>("Author", static m => m.AuthorOptions, static name => name, static m => m.FilterAuthor, static (m, name) => m.FilterAuthor = name),
        FormSettings.Choice<AnnotationsViewModel>("Sort by", AnnotationsViewModel.SortOptions, static m => m.SortOrder, static (m, value) => m.SortOrder = value),
        FormSettings.Action<AnnotationsViewModel>("Show all the notes", static m => m.ClearFilterCommand).With(ChromeIcon.Filter),
    ];

    /// <summary>The rows of the window that finds a tab among many: every open document, to go to with one press.</summary>
    private static readonly FormSetting<MainViewModel>[] TabFinderRows =
    [
        FormSettings.Field<MainViewModel>("Words in the name or the folder", static m => m.TabQuery, static (m, text) => m.TabQuery = text),
        FormSettings.Note<MainViewModel>("Choose the document to show."),
        FormSettings.Picker<MainViewModel, DocumentTabViewModel>(
            static m => [..m.FoundTabs],
            static tab => tab.HasUnsavedChanges ? UiText.Format("{0} (not saved)", tab.Title) : tab.Title,
            static (m, tab) =>
            {
                m.SelectedTab = tab;
                m.IsTabFinderOpen = false;
            }),
    ];

    /// <summary>Makes the window that opens a document locked with a certificate. It closes when the document opens.</summary>
    /// <param name="tab">The tab whose document is locked.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest CertificateLock(DocumentTabViewModel tab) => FormSettings.Window(
        UiText.Format("Open {0}", tab.FileName),
        tab,
        CertificateLockRows,
        Signal.Merge(
            tab.WhenChanged(static t => t.CertificatePathEntry).AsSignal(),
            tab.WhenChanged(static t => t.CertificatePasswordEntry).AsSignal(),
            tab.WhenChanged(static t => t.CertificateError).AsSignal())) with
    {
        Answered = tab.WhenChanged(static t => t.NeedsCertificate).Where(static needed => !needed).Select(static _ => true),
    };

    /// <summary>Makes the window of a note on the page. It closes when the note is kept, removed or put away.</summary>
    /// <param name="marks">The marking tools of the tab shown.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest NoteBubble(AnnotationsViewModel marks) => FormSettings.Window(
        UiText.Get("Note"),
        marks,
        BubbleRows,
        Signal.Merge(
            marks.WhenChanged(static m => m.BubbleNoteText).AsSignal(),
            marks.WhenChanged(static m => m.BubbleReplyText).AsSignal(),
            marks.WhenChanged(static m => m.IsReplyingInBubble).AsSignal(),
            marks.WhenChanged(static m => m.ActiveBubble).AsSignal())) with
    {
        Answered = marks.WhenChanged(static m => m.ActiveBubble).Where(static bubble => bubble is null).Select(static _ => true),
    };

    /// <summary>Makes the window of Focus Mode's text settings. It closes when it is closed; the settings apply as they are changed.</summary>
    /// <param name="focus">Focus Mode's view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest FocusSettings(FocusModeViewModel focus) => FormSettings.Window(UiText.Get("Text settings"), focus, FocusRows);

    /// <summary>Makes the window that filters and sorts the list of notes and marks. It closes when it is closed; the list changes as the choices do.</summary>
    /// <param name="marks">The marking tools of the tab shown.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest NoteFilter(AnnotationsViewModel marks) => FormSettings.Window(UiText.Get("Filter and sort the notes"), marks, NoteFilterRows);

    /// <summary>Makes the window that finds a tab among many. It closes when a tab is chosen.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest TabFinder(MainViewModel main) => FormSettings.Window(
        UiText.Get("Go to a document"),
        main,
        TabFinderRows,
        Signal.Merge(
            ((IList)main.Tabs).CollectionChanges.AsSignal(),
            ((IList)main.FoundTabs).CollectionChanges.AsSignal(),
            main.WhenChanged(static m => m.TabQuery).AsSignal())) with
    {
        Answered = main.WhenChanged(static m => m.IsTabFinderOpen).Where(static open => !open).Select(static _ => true),
    };

    /// <summary>Makes the signatures window.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Signatures(SignaturesViewModel model) =>
        FormSettings.Window(UiText.Format("Signatures of {0}", model.FileName), model, SignatureRows, WithList(model, model.Signatures));

    /// <summary>Makes the window that signs with a certificate. The view model answers when its sign command is done.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest CertificateSign(CertificateSignViewModel model) =>
        FormSettings.Window(UiText.Get("Sign with a certificate"), model, CertificateRows, WithList(model, model.Remembered)) with { Answered = model.Answered };

    /// <summary>Makes the window that signs with a key held somewhere else.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest ExternalSign(ExternalSignViewModel model) => FormSettings.Window(UiText.Format("Sign {0} with your own tool", model.FileName), model, ExternalRows);

    /// <summary>
    /// Makes the window that makes the mark of a hand signature or of initials. The view model answers when the mark is chosen. A drawn
    /// mark is drawn with the pointer on a pad beside the rows.
    /// </summary>
    /// <param name="model">The view model.</param>
    /// <param name="picture">Makes the picture of a mark that came from a picture file, for the room beside the rows.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest SignatureMark(SignatureMarkViewModel model, Func<SurfaceSize, IRenderSurface?> picture) => FormSettings.Window(model.Title, model, MarkRows) with
    {
        // The button that uses the mark sits with Cancel along the bottom, and Enter presses it.
        AcceptText = model.UseText,
        CanAccept = () => model.Preview is not null,
        Answered = model.Answered,
        Picture = picture,
        PreviewText = () => model.IsTyped ? model.Preview?.Text : null,
        StartsTyping = true,
        Pad = strokes => strokes.Where(static stroke => stroke.Done).Select(static stroke => stroke.Points).InvokeCommand(model.AddStrokeCommand),
        PadShown = () => model.IsDrawn,
        PadSeed = area => PadInks.FromMark(model.Drawing, area),
        PadCleared = model.WhenChanged(static m => m.Drawing).Where(static drawing => drawing is null).AsSignal(),
    };

    /// <summary>Makes the window that adds a watermark, a header, a footer, page numbers, Bates numbers, a background or a crop to the pages. The view model answers when it is accepted.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest PageDecoration(PageDecorationViewModel model) => FormSettings.Window(model.Title, model, DecorationRows) with { Answered = model.Answered };

    /// <summary>Makes the window that lists the pages whose words differ from another document.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Compare(CompareViewModel model) => FormSettings.Window(model.Title, model, CompareRows);

    /// <summary>Makes the window that takes hidden information out of the document. The view model answers when it is accepted.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Sanitize(SanitizeViewModel model) => FormSettings.Window(model.Title, model, SanitizeRows) with { Answered = model.Answered };

    /// <summary>Makes the window that numbers a run of pages i, ii, iii or A-1. The view model answers when it is accepted.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest PageLabels(PageLabelsViewModel model) => FormSettings.Window(model.Title, model, LabelRows) with { Answered = model.Answered };

    /// <summary>Makes the window that sets how typed text looks. The view model answers when the look is accepted.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest TextProperties(TextPropertiesViewModel model) => FormSettings.Window(UiText.Get("Text"), model, TextRows) with { Answered = model.Answered };

    /// <summary>Makes the window that plays sound or film. The picture of the film is shown beside the buttons.</summary>
    /// <param name="player">The player.</param>
    /// <param name="picture">Gets the latest picture of the film as a surface, in the room it is given.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest MediaPlayer(MediaPlayerViewModel player, Func<SurfaceSize, IRenderSurface?> picture) =>
        FormSettings.Window(UiText.Get("Sound and film"), player, MediaRows) with { Picture = picture };

    /// <summary>Makes the window that changes the chosen tag of the document.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest TagEditor(TagsViewModel model) => FormSettings.Window(UiText.Get("Change a tag"), model, TagRows);

    /// <summary>Makes the window that searches a folder of documents.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest FolderSearch(FolderSearchViewModel model) =>
        FormSettings.Window(UiText.Get("Search a folder of documents"), model, FolderRows, WithList(model, model.Results));

    /// <summary>Follows a view model and one list it holds, so a window is laid out again when either changes.</summary>
    /// <typeparam name="T">The kind of view model.</typeparam>
    /// <param name="model">The view model.</param>
    /// <param name="list">The list.</param>
    /// <returns>A signal for each change.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RxVoid> WithList<T>(T model, IList list)
        where T : class, IReactiveObject =>
        Signal.Merge(model.GetChangedObservable().AsSignal(), list.CollectionChanges.AsSignal());

    /// <summary>Describes one signature in a line.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>Who signed, what the check found and its detail.</returns>
    private static string Describe(DocumentSignature signature) =>
        signature.Detail.Length > 0 ? $"{signature.SignerName}: {signature.Summary}. {signature.Detail}" : $"{signature.SignerName}: {signature.Summary}";

    /// <summary>Writes a number as it is shown in a typing box.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Number(decimal value) => value.ToString(CultureInfo.CurrentCulture);

    /// <summary>Reads a number as it is typed.</summary>
    /// <param name="text">The typed text.</param>
    /// <param name="current">The number kept when the text is not a number.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static decimal Number(string text, decimal current) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) ? value : current;

    /// <summary>Writes a fractional number as it is shown in a typing box.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Number(double value) => value.ToString(CultureInfo.CurrentCulture);

    /// <summary>Reads a fractional number as it is typed.</summary>
    /// <param name="text">The typed text.</param>
    /// <param name="current">The number kept when the text is not a number.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Number(string text, double current) => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) && double.IsFinite(value) ? value : current;

    /// <summary>Reads a whole number as it is typed.</summary>
    /// <param name="text">The typed text.</param>
    /// <param name="current">The number kept when the text is not a whole number.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WholeNumber(string text, int current) => int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) ? value : current;

    /// <summary>Makes a setting of a tag that is chosen from a list of words.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="choices">Gets the words to choose from.</param>
    /// <param name="get">Gets the word chosen.</param>
    /// <param name="set">Chooses a word.</param>
    /// <returns>The setting.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FormSetting<TagsViewModel> TagChoice(string label, Func<TagsViewModel, string[]> choices, Func<TagsViewModel, string?> get, Action<TagsViewModel, string> set) =>
        FormSettings.Pick(label, choices, static word => word, get, set);

    /// <summary>Gets the custom tag type that is chosen in the list of custom types.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>The words that name the custom type and what it stands for, or <see langword="null"/> when none is chosen.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? RoleMappingOf(TagsViewModel model) => (uint)model.RoleMappingIndex < (uint)model.RoleMappings.Length ? model.RoleMappings[model.RoleMappingIndex] : null;

    /// <summary>Gets the tag the chosen tag is to move into.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>The words that name the target, or <see langword="null"/> when none is chosen.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? MoveTargetOf(TagsViewModel model) => model.MoveTargetIndex >= 0 ? model.MoveTargets[model.MoveTargetIndex] : null;

    /// <summary>Finds a choice in a list of choices.</summary>
    /// <param name="choices">The choices.</param>
    /// <param name="choice">The choice.</param>
    /// <returns>The place of the choice in the list, or -1 when it is not there.</returns>
    private static int IndexOf(ReadOnlySpan<string> choices, string choice)
    {
        for (var i = 0; i < choices.Length; i++)
        {
            if (string.Equals(choices[i], choice, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
