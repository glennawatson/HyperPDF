// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.FormFilling;

/// <summary>Prepares a form: adds fields to the page, removes them and changes what they ask of the person filling them in.</summary>
[DebuggerDisplay("FormDesignViewModel: {Status}")]
public sealed partial class FormDesignViewModel : ReactiveObject, IDisposable
{
    /// <summary>The width of a new text field or drop-down list, as a share of the page's width.</summary>
    private const float FieldWidthShare = 0.4F;

    /// <summary>The height of a new text field or drop-down list, in points.</summary>
    private const float FieldHeight = 24F;

    /// <summary>The side of a new check box, in points.</summary>
    private const float CheckBoxSide = 18F;

    /// <summary>The width of a new button, in points.</summary>
    private const float ButtonWidth = 120F;

    /// <summary>The height of a new button, in points.</summary>
    private const float ButtonHeight = 28F;

    /// <summary>Half of a length.</summary>
    private const float Half = 0.5F;

    /// <summary>The words of a new text field's tooltip.</summary>
    private const string FieldPrefix = "Field";

    /// <summary>The tab holding the document.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Cancelled when the tab closes.</summary>
    private readonly CancellationDisposable _lifetime = new();

    /// <summary>Initializes a new instance of the <see cref="FormDesignViewModel"/> class.</summary>
    /// <param name="owner">The tab holding the document.</param>
    public FormDesignViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets what the last change did, in plain words.</summary>
    [Reactive]
    public partial string Status { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the open document can have its form prepared.</summary>
    public bool IsAvailable => _owner.IsLoaded && Designer is not null;

    /// <summary>Gets what is said when a change asks for a field and none is picked, in the language in use now.</summary>
    private static string PickFirst => UiText.Get("Click a field to pick it first.");

    /// <summary>Gets the form designer of the open document.</summary>
    private IFormDesigner? Designer => DocumentFeature<IFormDesigner>.Get(_owner.TryGetDocument());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _lifetime.Dispose();

    /// <summary>Finds a field name that no field of the form has: Field1, Field2 and on.</summary>
    /// <param name="fields">The form's fields.</param>
    /// <returns>The name.</returns>
    private static string NextName(IReadOnlyList<FormFieldInfo> fields)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            _ = taken.Add(field.Name);
        }

        var number = 1;
        var name = string.Concat(FieldPrefix, number.ToString(CultureInfo.InvariantCulture));
        while (taken.Contains(name))
        {
            number++;
            name = string.Concat(FieldPrefix, number.ToString(CultureInfo.InvariantCulture));
        }

        return name;
    }

    /// <summary>Adds a text field in the middle of the page being read.</summary>
    /// <returns>A signal that ends once the field is added.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> AddTextField() => Add(FormFieldKind.Text, FieldWidthShare, FieldHeight, null);

    /// <summary>Adds a check box in the middle of the page being read.</summary>
    /// <returns>A signal that ends once the field is added.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> AddCheckBox() => Add(FormFieldKind.CheckBox, 0F, CheckBoxSide, null);

    /// <summary>Adds a drop-down list in the middle of the page being read.</summary>
    /// <returns>A signal that ends once the field is added.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> AddDropDown() => Add(FormFieldKind.ComboBox, FieldWidthShare, FieldHeight, [new("First", "First"), new("Second", "Second")]);

    /// <summary>Adds a button in the middle of the page being read.</summary>
    /// <returns>A signal that ends once the field is added.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> AddButton() => Add(FormFieldKind.PushButton, 0F, ButtonHeight, null);

    /// <summary>Removes the field that has the keyboard focus on the page.</summary>
    /// <returns>A signal that ends once the field is removed.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> RemoveFocused() => Run(
        static (designer, field) => designer.RemoveField(field.Name),
        UiText.Get("Removed the field."),
        PickFirst);

    /// <summary>Makes the focused field one that must be filled in, or one that need not be.</summary>
    /// <returns>A signal that ends once the field is changed.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> ToggleRequired() => Run(
        static (designer, field) => designer.ChangeField(
            field.Name,
            field.IsRequired ? new() { RemoveFlags = FormDesignFlags.Required } : new() { AddFlags = FormDesignFlags.Required }),
        UiText.Get("Changed whether the field must be filled in."),
        PickFirst);

    /// <summary>Makes the focused field one that cannot be changed, or one that can.</summary>
    /// <returns>A signal that ends once the field is changed.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> ToggleReadOnly() => Run(
        static (designer, field) => designer.ChangeField(
            field.Name,
            field.IsReadOnly ? new() { RemoveFlags = FormDesignFlags.ReadOnly } : new() { AddFlags = FormDesignFlags.ReadOnly }),
        UiText.Get("Changed whether the field can be changed."),
        PickFirst);

    /// <summary>Adds a field of a kind in the middle of the page being read.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="widthShare">The field's width as a share of the page's width, or 0 to use the height as the width.</param>
    /// <param name="height">The field's height in points.</param>
    /// <param name="options">The choices of a drop-down list, or <see langword="null"/>.</param>
    /// <returns>A cold signal that ends once the field is added.</returns>
    private IObservable<RxVoid> Add(FormFieldKind kind, float widthShare, float height, FormChoiceOption[]? options)
    {
        if (Designer is not { } designer)
        {
            return Signal.Return(RxVoid.Default);
        }

        var page = Math.Clamp(_owner.CurrentPageIndex, 0, Math.Max(0, _owner.Source.PageSizes.Length - 1));
        var size = _owner.Source.PageSizes[page];
        var width = widthShare > 0 ? size.Width * widthShare : height;
        if (kind == FormFieldKind.PushButton)
        {
            width = ButtonWidth;
        }

        var bounds = new PageRect((size.Width - width) * Half, (size.Height - height) * Half, width, height);

        return designer.GetFields()
            .SwitchMap(fields => Create(designer, NextName(fields), new(kind, string.Empty, [new(page, bounds)]) { Options = options ?? [] }, page))
            .TakeUntil(_lifetime.Token)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Catch<RxVoid, OperationCanceledException>(Stopped);
    }

    /// <summary>Creates a field with a name no other field has.</summary>
    /// <param name="designer">The designer.</param>
    /// <param name="name">The field's name.</param>
    /// <param name="template">The field's design, which still needs its name.</param>
    /// <param name="page">The page the field is on.</param>
    /// <returns>A cold signal that ends once the field is created.</returns>
    private IObservable<RxVoid> Create(IFormDesigner designer, string name, FormFieldDesign template, int page)
    {
        var design = template with { Name = name, Caption = template.Kind == FormFieldKind.PushButton ? name : null };
        return designer.CreateField(design)
            .Select(result => Finish(result, page, UiText.Format("Added the field {0}. Drag it to where it belongs.", name)));
    }

    /// <summary>Runs a change on the field that has the focus.</summary>
    /// <param name="edit">The change, given the designer and the field.</param>
    /// <param name="done">What to say when it worked.</param>
    /// <param name="nothing">What to say when no field has the focus.</param>
    /// <returns>A cold signal that ends once the change is made.</returns>
    private IObservable<RxVoid> Run(Func<IFormDesigner, FormField, IObservable<DocumentEditResult>> edit, string done, string nothing)
    {
        if (Designer is not { } designer)
        {
            return Signal.Return(RxVoid.Default);
        }

        if (_owner.Forms.Focused is not { } field)
        {
            Status = nothing;
            return Signal.Return(RxVoid.Default);
        }

        return edit(designer, field)
            .TakeUntil(_lifetime.Token)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(result => Finish(result, field.PageIndex, done))
            .Catch<RxVoid, OperationCanceledException>(Stopped);
    }

    /// <summary>Gives up quietly when the tab closed, and fails otherwise.</summary>
    /// <param name="error">The cancellation.</param>
    /// <returns>A signal that ends at once, or fails with the same error.</returns>
    private IObservable<RxVoid> Stopped(OperationCanceledException error) =>
        _lifetime.Token.IsCancellationRequested ? Signal.Return(RxVoid.Default) : Signal.Throw<RxVoid>(error);

    /// <summary>Shows what a change did, and has the pages drawn again when it worked.</summary>
    /// <param name="result">The outcome.</param>
    /// <param name="page">The page that changed.</param>
    /// <param name="done">What to say when it worked.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Finish(DocumentEditResult result, int page, string done)
    {
        if (result.Succeeded)
        {
            _owner.ReportPageChange(page);
            _owner.Pages.Refresh();
        }

        Status = result.Succeeded ? done : result.Problem ?? UiText.Get("The form could not be changed.");
        return RxVoid.Default;
    }
}
