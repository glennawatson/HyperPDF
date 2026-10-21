// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>
/// Fill &amp; Sign for one tab. Form fields are filled directly on the page. A signature or initials is made (typed,
/// drawn or from a picture), previewed on the page, moved and resized with the keyboard or pointer, then placed.
/// Escape cancels placing without changing the document. Remembering a mark is the user's choice.
/// </summary>
[DebuggerDisplay("FillAndSignViewModel: Fill & Sign: {IsActive}, placing {Placement}")]
public sealed partial class FillAndSignViewModel : ReactiveObject
{
    /// <summary>The steps shown while a mark is being placed.</summary>
    public static readonly string PlacingHint = "Arrow keys move it; hold Shift for small steps. + and − resize it. Page Up and Page Down change page. Enter places it. Escape cancels.";

    /// <summary>The hint shown while filling forms.</summary>
    public static readonly string FillingHint = "Click a form field to fill it in, or click anywhere on the page and type. Tab moves between fields.";

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The services, for the remembered marks.</summary>
    private readonly AppServices _services;

    /// <summary>Whether a mark is being placed.</summary>
    private readonly IObservable<bool> _isPlacing;

    /// <summary>Whether the last placed mark can still be adjusted or removed.</summary>
    private readonly IObservable<bool> _canChangePlaced;

    /// <summary>The mark placed last, with the annotation it became, so it can be adjusted or removed.</summary>
    private (PageAnnotation Annotation, SignaturePlacement Placement)? _lastPlaced;

    /// <summary>The placed mark being adjusted; it is put back if adjusting is cancelled.</summary>
    private SignaturePlacement? _adjusting;

    /// <summary>Initializes a new instance of the <see cref="FillAndSignViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The services.</param>
    public FillAndSignViewModel(DocumentTabViewModel owner, AppServices services)
    {
        (_owner, _services) = (owner, services);
        _isPlacing = this.WhenChanged(static vm => vm.Placement).Select(static placement => placement is not null);
        _canChangePlaced = this.WhenChanged(static vm => vm.HasPlacedMark, static vm => vm.Placement, static (placed, placement) => placed && placement is null);
    }

    /// <summary>Gets the interaction showing the window that makes a signature or initials; the output says whether to use it.</summary>
    public Interaction<SignatureMarkViewModel, bool> MarkInteraction { get; } = new();

    /// <summary>Gets or sets a value indicating whether the Fill &amp; Sign tool row is shown.</summary>
    [Reactive]
    public partial bool IsActive { get; set; }

    /// <summary>Gets the mark being placed, or <see langword="null"/> when none is.</summary>
    [Reactive(nameof(Hint))]
    public partial SignaturePlacement? Placement { get; private set; }

    /// <summary>Gets a value indicating whether the last placed mark can be adjusted or removed.</summary>
    [Reactive]
    public partial bool HasPlacedMark { get; private set; }

    /// <summary>Gets the hint shown in the tool row.</summary>
    public string Hint => Placement is null ? FillingHint : PlacingHint;

    /// <summary>Gets the tab's annotation state.</summary>
    private AnnotationsViewModel Annotations => _owner.Annotations;

    /// <summary>Starts placing a mark, centred on the current page.</summary>
    /// <param name="mark">The mark.</param>
    /// <returns><see langword="true"/> when placing started.</returns>
    public bool BeginPlacement(SignatureMark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        var sizes = _owner.Source.PageSizes;
        if (sizes.Length == 0 || !mark.IsValid || !Annotations.CanAnnotate)
        {
            return false;
        }

        var page = Math.Clamp(_owner.CurrentPageIndex, 0, sizes.Length - 1);
        Show(new(page, SignatureMarkLayout.Start(mark, sizes[page]), mark));
        return true;
    }

    /// <summary>Moves the mark being placed, keeping it on the page.</summary>
    /// <param name="dx">The move to the right, in points.</param>
    /// <param name="dy">The move down, in points.</param>
    /// <returns><see langword="true"/> when a mark is being placed.</returns>
    public bool Move(float dx, float dy)
    {
        if (Placement is not { } placement)
        {
            return false;
        }

        Placement = placement with { Bounds = SignatureMarkLayout.Move(placement.Bounds, dx, dy, PageSize(placement.Page)) };
        return true;
    }

    /// <summary>Grows or shrinks the mark being placed about its centre.</summary>
    /// <param name="factor">The scale: more than 1 grows.</param>
    /// <returns><see langword="true"/> when a mark is being placed.</returns>
    public bool Resize(float factor)
    {
        if (Placement is not { } placement)
        {
            return false;
        }

        Placement = placement with { Bounds = SignatureMarkLayout.Resize(placement.Bounds, factor, PageSize(placement.Page)) };
        return true;
    }

    /// <summary>Moves the mark being placed to another page, at the same spot.</summary>
    /// <param name="delta">The pages to move: 1 for the next page, -1 for the previous.</param>
    /// <returns><see langword="true"/> when a mark is being placed.</returns>
    public bool MoveToPage(int delta)
    {
        if (Placement is not { } placement)
        {
            return false;
        }

        var page = Math.Clamp(placement.Page + delta, 0, _owner.Source.PageSizes.Length - 1);
        if (page != placement.Page)
        {
            Show(placement with { Page = page, Bounds = SignatureMarkLayout.Clamp(placement.Bounds, PageSize(page)) });
        }

        return true;
    }

    /// <summary>Centres the mark being placed on a point, for example where the page was clicked.</summary>
    /// <param name="page">The page.</param>
    /// <param name="centre">The point in page space.</param>
    /// <returns><see langword="true"/> when a mark is being placed.</returns>
    public bool MoveTo(int page, PagePoint centre)
    {
        if (Placement is not { } placement || (uint)page >= (uint)_owner.Source.PageSizes.Length)
        {
            return false;
        }

        Placement = placement with { Page = page, Bounds = SignatureMarkLayout.CentreOn(placement.Bounds, centre, PageSize(page)) };
        return true;
    }

    /// <summary>Places the mark being previewed on the page.</summary>
    /// <returns><see langword="true"/> when a mark was placed.</returns>
    public bool Commit()
    {
        if (Placement is not { } placement)
        {
            return false;
        }

        Placement = null;
        _adjusting = null;
        if (Annotations.PlaceMark(placement.Page, placement.Bounds, placement.Mark) is not { } placed)
        {
            _owner.Notice = "The mark could not be placed on this page.";
            return false;
        }

        _lastPlaced = (placed, placement);
        HasPlacedMark = true;
        return true;
    }

    /// <summary>Stops placing without changing the document; a mark being adjusted goes back where it was.</summary>
    /// <returns><see langword="true"/> when a mark was being placed.</returns>
    public bool Cancel()
    {
        if (Placement is null)
        {
            return false;
        }

        Placement = null;
        if (_adjusting is not { } original)
        {
            return true;
        }

        _adjusting = null;
        if (Annotations.PlaceMark(original.Page, original.Bounds, original.Mark) is { } placed)
        {
            _lastPlaced = (placed, original);
            HasPlacedMark = true;
        }

        return true;
    }

    /// <summary>Picks what to remember after the mark window closes.</summary>
    /// <param name="request">The window's state.</param>
    /// <param name="mark">The mark the user chose to use, or <see langword="null"/> when they cancelled.</param>
    /// <param name="remembered">The mark remembered before the window opened.</param>
    /// <returns>The mark to remember, or <see langword="null"/> to remember none.</returns>
    private static SignatureMark? ToRemember(SignatureMarkViewModel request, SignatureMark? mark, SignatureMark? remembered)
    {
        if (mark is not null)
        {
            return request.Remember ? mark : null;
        }

        // Forgetting is its own choice, so it holds even when the window is cancelled.
        return request.Forgotten ? null : remembered;
    }

    /// <summary>Gets a page's size, or Letter when it is unknown.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The size in points.</returns>
    private PageSize PageSize(int page)
    {
        var sizes = _owner.Source.PageSizes;
        return (uint)page < (uint)sizes.Length ? sizes[page] : Core.Geometry.PageSize.Letter;
    }

    /// <summary>Shows a placement and scrolls it into view.</summary>
    /// <param name="placement">The placement.</param>
    private void Show(SignaturePlacement placement)
    {
        Annotations.Tool = AnnotationTool.Select;
        Annotations.Select(null);
        IsActive = true;
        Placement = placement;
        _owner.ShowArea(placement.Page, placement.Bounds);
    }

    /// <summary>Checks the last placed mark is still on the page as it was placed.</summary>
    /// <returns>The mark, or <see langword="null"/> when it was removed or changed since.</returns>
    private (PageAnnotation Annotation, SignaturePlacement Placement)? CurrentPlaced()
    {
        if (_lastPlaced is { } placed && Annotations.Find(placed.Annotation.PageIndex, placed.Annotation.Index) == placed.Annotation)
        {
            return placed;
        }

        _lastPlaced = null;
        HasPlacedMark = false;
        return null;
    }

    /// <summary>Asks for a signature, offering the remembered one, then starts placing it.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task Signature() => CreateMarkAsync(SignatureMarkKind.Signature);

    /// <summary>Asks for initials, offering the remembered ones, then starts placing them.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task Initials() => CreateMarkAsync(SignatureMarkKind.Initials);

    /// <summary>Shows the window that makes a mark, applies the user's remember choice, then starts placing the mark.</summary>
    /// <param name="kind">Signature or initials.</param>
    /// <returns>A task.</returns>
    private async Task CreateMarkAsync(SignatureMarkKind kind)
    {
        _ = Cancel();
        using var request = new SignatureMarkViewModel(kind, _services.SignatureMarks.Get(kind), _services.Head.LoadImage);
        var accepted = await MarkInteraction.Handle(request).ToTask().ConfigureAwait(true);
        var remembered = _services.SignatureMarks.Get(kind);
        var mark = accepted ? request.Result : null;
        var keep = ToRemember(request, mark, remembered);
        if (!ReferenceEquals(keep, remembered))
        {
            _services.SignatureMarks.Set(kind, keep);
            _services.SaveSignatureMarks();
        }

        if (mark is not null)
        {
            _ = BeginPlacement(mark);
        }
    }

    /// <summary>Places the mark being previewed.</summary>
    [ReactiveCommand(CanExecute = nameof(_isPlacing))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Place() => _ = Commit();

    /// <summary>Stops placing without changing the document.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CancelPlacement() => _ = Cancel();

    /// <summary>Makes the mark being placed bigger.</summary>
    [ReactiveCommand(CanExecute = nameof(_isPlacing))]
    private void Bigger() => _ = Resize(SignatureMarkLayout.ResizeStep);

    /// <summary>Makes the mark being placed smaller.</summary>
    [ReactiveCommand(CanExecute = nameof(_isPlacing))]
    private void Smaller() => _ = Resize(1 / SignatureMarkLayout.ResizeStep);

    /// <summary>Picks up the last placed mark so it can be moved or resized again.</summary>
    [ReactiveCommand(CanExecute = nameof(_canChangePlaced))]
    private void Adjust()
    {
        if (CurrentPlaced() is not { } placed)
        {
            return;
        }

        Annotations.Delete(placed.Annotation);
        _lastPlaced = null;
        HasPlacedMark = false;
        _adjusting = placed.Placement;
        Show(placed.Placement);
    }

    /// <summary>Removes the last placed mark from the document.</summary>
    [ReactiveCommand(CanExecute = nameof(_canChangePlaced))]
    private void RemoveMark()
    {
        if (CurrentPlaced() is not { } placed)
        {
            return;
        }

        Annotations.Delete(placed.Annotation);
        _lastPlaced = null;
        HasPlacedMark = false;
    }

    /// <summary>Shows the tools; annotation tools are put away so only one tool row is ever shown.</summary>
    [ReactiveCommand]
    private void Start()
    {
        // As on paper, clicking anywhere types there; form fields are still filled by clicking them.
        Annotations.IsAnnotating = false;
        Annotations.Tool = AnnotationTool.Text;
        IsActive = true;
    }

    /// <summary>Shows the tools when hidden and hides them when shown, as the Fill &amp; Sign button does.</summary>
    [ReactiveCommand]
    private void Toggle()
    {
        if (IsActive)
        {
            Done();
            return;
        }

        Start();
    }

    /// <summary>Hides the tools, cancelling any placing.</summary>
    [ReactiveCommand]
    private void Done()
    {
        _ = Cancel();
        _ = Annotations.CommitText();
        Annotations.Tool = AnnotationTool.Select;
        IsActive = false;
    }
}
