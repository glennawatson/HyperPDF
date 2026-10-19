// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads action types and destinations.</summary>
public static class PdfDocumentActionTypes
{
    /// <summary>The play operation of a movie action that names none.</summary>
    private const string DefaultMovieOperation = "Play";

    /// <summary>Reads an action that is not a viewer-facing kind.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <param name="subtype">The <c>/S</c> name.</param>
    /// <returns>The action; an <see cref="UnsupportedAction"/> for names the library does not read.</returns>
    internal static PdfAction ReadExtendedAction(PdfDocument document, PdfDictionary action, string subtype) => subtype switch
    {
        "SubmitForm" => new SubmitFormAction(
PdfDocumentFileSpecs.ReadFileSpec(document, action.Get(KnownName.F)),
PdfDocumentActionTypes.ReadFieldNames(action.Array("Fields")),
action.Int("Flags", 0)),
        "ResetForm" => new ResetFormAction(PdfDocumentActionTypes.ReadFieldNames(action.Array("Fields")), action.Int("Flags", 0)),
        "ImportData" => new ImportDataAction(PdfDocumentFileSpecs.ReadFileSpec(document, action.Get(KnownName.F))),
        "Hide" => new HideAction(PdfDocumentActionTypes.ReadHideTargets(action.Get(KnownName.T)), action.Flag("H", true)),
        "Sound" => PdfDocumentActionTypes.ReadSoundAction(action),
        "Movie" => new MovieAction(action.Text("T"), action.Dict("Annotation"), action.NameText("Operation") ?? PdfDocumentActionTypes.DefaultMovieOperation),
        "Rendition" => PdfDocumentActionTypes.ReadRenditionAction(document, action),
        "Trans" => action.Dict("Trans") is { } trans ? new TransAction(PdfDocumentViewerPreferences.ReadTransition(trans, null)) : new UnsupportedAction(subtype),
        "GoTo3DView" => PdfDocumentActionTypes.ReadGoTo3DView(document, action),
        "RichMediaExecute" => PdfDocumentActionTypes.ReadRichMediaExecute(action),
        "SetOCGState" => new SetOcgStateAction(PdfDocumentActionTypes.ReadOcgStates(action.Array("State")), action.Flag("PreserveRB", true)),
        "Thread" => PdfDocumentActionTypes.ReadThreadAction(document, action),
        "GoToDp" => new GoToDpAction(action.Dict("Dp")),
        _ => new UnsupportedAction(subtype),
    };

    /// <summary>Reads a field name list: strings, or field dictionaries given by their qualified names.</summary>
    /// <param name="fields">The <c>/Fields</c> array, or null.</param>
    /// <returns>The names; empty when the array is missing.</returns>
    private static string[] ReadFieldNames(PdfArray? fields)
    {
        if (fields is null)
        {
            return [];
        }

        var names = new List<string>(fields.Count);
        for (var i = 0; i < fields.Count; i++)
        {
            if (PdfDocumentActionTypes.ReadFieldName(fields.Get(i)) is { } name)
            {
                names.Add(name);
            }
        }

        return [.. names];
    }

    /// <summary>Reads one field reference: a string, or a field or annotation dictionary.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The qualified name, or null.</returns>
    private static string? ReadFieldName(PdfValue value)
    {
        if (value.Kind == PdfKind.String)
        {
            return PdfText.Decode(value.AsStringBytes());
        }

        return value.AsDictionary() is { } field ? PdfDocumentActionTypes.QualifiedName(field) : null;
    }

    /// <summary>Builds a field's fully qualified name by joining the <c>/T</c> of its parents with dots.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The name, or null when no ancestor has a partial name.</returns>
    private static string? QualifiedName(PdfDictionary field)
    {
        var parts = new List<string>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        for (var node = field; node is not null && parts.Count < PdfDocumentContent.MaxFieldDepth && visited.Add(node); node = node.GetDictionary(KnownName.Parent))
        {
            if (node.GetText(KnownName.T) is { Length: > 0 } partial)
            {
                parts.Add(partial);
            }
        }

        parts.Reverse();
        return parts.Count == 0 ? null : string.Join('.', parts);
    }

    /// <summary>Reads the <c>/T</c> of a hide action: one target or an array.</summary>
    /// <param name="target">The value.</param>
    /// <returns>The target names.</returns>
    private static string[] ReadHideTargets(PdfValue target)
    {
        if (target.AsArray() is { } array)
        {
            return PdfDocumentActionTypes.ReadFieldNames(array);
        }

        return PdfDocumentActionTypes.ReadFieldName(target) is { } single ? [single] : [];
    }

    /// <summary>Reads a sound action.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action.</returns>
    private static SoundAction ReadSoundAction(PdfDictionary action)
    {
        var sound = action.Stream("Sound");
        return new(sound is null ? null : PdfDocumentMedia.ReadSound(sound), action.Num("Volume", 1), action.Flag("Synchronous", false), action.Flag("Repeat", false), action.Flag("Mix", false));
    }

    /// <summary>Reads a rich media execute action.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action.</returns>
    private static RichMediaExecuteAction ReadRichMediaExecute(PdfDictionary action)
    {
        var command = action.Dict("CMD");
        return new(action.Dict("TA"), action.Dict("TI"), command?.Text("C"), command?.Array("A"));
    }

    /// <summary>Reads a set-OCG-state array: a mode name followed by the groups it applies to, repeated.</summary>
    /// <param name="state">The <c>/State</c> array, or null.</param>
    /// <returns>The steps.</returns>
    private static PdfOcgStateChange[] ReadOcgStates(PdfArray? state)
    {
        var changes = new List<PdfOcgStateChange>();
        string? mode = null;
        var groups = new List<PdfObjectId>();
        for (var i = 0; state is not null && i < state.Count; i++)
        {
            if (state.NameText(i) is { } name)
            {
                PdfDocumentActionTypes.FlushStep(changes, mode, groups);
                mode = name;
            }
            else if (state.GetRaw(i).IsReference)
            {
                groups.Add(state.GetRaw(i).AsReference());
            }
        }

        PdfDocumentActionTypes.FlushStep(changes, mode, groups);
        return [.. changes];
    }

    /// <summary>Ends the current step of a state array.</summary>
    /// <param name="changes">The finished steps.</param>
    /// <param name="mode">The step's mode, or null before the first mode.</param>
    /// <param name="groups">The groups collected for the step; cleared.</param>
    private static void FlushStep(List<PdfOcgStateChange> changes, string? mode, List<PdfObjectId> groups)
    {
        if (mode is not null)
        {
            changes.Add(new(mode, [.. groups]));
        }

        groups.Clear();
    }

    /// <summary>Reads a rendition action.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action.</returns>
    private static RenditionAction ReadRenditionAction(PdfDocument document, PdfDictionary action)
    {
        var rendition = action.Dict("R");
        return new(action.Int("OP", -1), rendition is null ? null : PdfDocumentMedia.ReadRendition(document, rendition), action.Dict("AN"), PdfDocumentFileSpecs.ReadScript(action.Get(KnownName.JS)));
    }

    /// <summary>Reads a 3D view action.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action.</returns>
    private static GoTo3DViewAction ReadGoTo3DView(PdfDocument document, PdfDictionary action)
    {
        var view = action.Value("V");
        var viewDictionary = view.AsDictionary();
        var selector = viewDictionary is null ? view.ScalarText(document.Objects) : null;
        return new(action.Dict("TA"), viewDictionary is null ? null : PdfDocumentMedia3D.Read3DView(viewDictionary), selector, action.NameText("C"));
    }

    /// <summary>Reads a thread action.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action.</returns>
    private static ThreadAction ReadThreadAction(PdfDocument document, PdfDictionary action)
    {
        var thread = action.Value("D");
        var bead = action.Value("B");
        return new(
PdfDocumentFileSpecs.ReadFileSpec(document, action.Get(KnownName.F)),
thread.AsDictionary(),
thread.IsNumber ? thread.AsInt32() : null,
thread.Kind == PdfKind.String ? PdfText.Decode(thread.AsStringBytes()) : null,
bead.IsNumber ? bead.AsInt32() : null);
    }
}
