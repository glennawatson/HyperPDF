// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads embedded go-to actions (ISO 32000-2 12.6.4.4) as data, without opening the target document.</summary>
internal static class EmbeddedGoToReader
{
    /// <summary>The list capacity for a target path with more than one step: a sibling path has two.</summary>
    private const int MultipleSteps = 2;

    /// <summary>Reads an embedded go-to action: its target steps, root file and destination in the target.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action.</returns>
    internal static EmbeddedGoToAction Read(PdfDocument document, PdfDictionary action)
    {
        var targets = EmbeddedGoToReader.ReadTargets(action.Dict("T"), out var loops);
        var destination = action.Value("D");
        var newWindow = action.Value("NewWindow");
        return new(!targets.IsEmpty ? targets[0].FileName : null)
        {
            File = EmbeddedGoToReader.ReadRootFile(document, action),
            Targets = targets,
            HasTargetLoop = loops,
            Destination = destination.AsArray() is { } array ? PdfDocumentNavigation.ReadOtherDocumentDestination(array) : null,
            NamedDestination = EmbeddedGoToReader.ReadName(document, destination),
            NewWindow = newWindow.Kind == PdfKind.Boolean ? newWindow.AsBoolean() : null,
        };
    }

    /// <summary>Reads the target steps by following nested <c>/T</c> entries.</summary>
    /// <param name="first">The action's target dictionary, or null.</param>
    /// <param name="loops">Whether the steps loop back or nest deeper than the reader follows.</param>
    /// <returns>The steps in order.</returns>
    private static ImmutableArray<PdfEmbeddedTarget> ReadTargets(PdfDictionary? first, out bool loops)
    {
        loops = false;
        if (first is null)
        {
            return [];
        }

        // Most actions have one step, so the list and the loop guard are only made when a second step exists.
        var second = first.Dict("T");
        if (second is null)
        {
            return [EmbeddedGoToReader.ReadTarget(first)];
        }

        var targets = new List<PdfEmbeddedTarget>(MultipleSteps) { EmbeddedGoToReader.ReadTarget(first) };
        HashSet<PdfDictionary> visited = [with(ReferenceEqualityComparer.Instance), first];
        for (var target = second; target is not null; target = target.Dict("T"))
        {
            if (targets.Count >= PdfLimits.MaxNesting || !visited.Add(target))
            {
                loops = true;
                break;
            }

            targets.Add(EmbeddedGoToReader.ReadTarget(target));
        }

        return [.. targets];
    }

    /// <summary>Reads one target dictionary.</summary>
    /// <param name="target">The target dictionary.</param>
    /// <returns>The step.</returns>
    private static PdfEmbeddedTarget ReadTarget(PdfDictionary target)
    {
        var page = target.Value("P");
        var annotation = target.Value("A");
        return new(
            target.NameText("R") ?? string.Empty,
            target.Text("N"),
            page.IsNumber ? page.AsInt32() : null,
            page.Kind == PdfKind.String ? PdfText.Decode(page.AsStringBytes()) : null,
            annotation.IsNumber ? annotation.AsInt32() : null,
            annotation.Kind == PdfKind.String ? PdfText.Decode(annotation.AsStringBytes()) : null);
    }

    /// <summary>Reads the root document of the target (<c>/F</c>), as a platform path unless it is a URL.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The file, or null when the source and target share a root.</returns>
    private static string? ReadRootFile(PdfDocument document, PdfDictionary action)
    {
        if (PdfDocumentFileSpecs.ReadFileSpec(document, action.Get(KnownName.F), out var isUrl) is not { } file)
        {
            return null;
        }

        return isUrl ? file : PdfDocumentFileSpecs.ToPlatformPath(file);
    }

    /// <summary>Reads a named destination: a name or a string.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The <c>/D</c> value.</param>
    /// <returns>The name, or null when the destination is not named.</returns>
    private static string? ReadName(PdfDocument document, PdfValue destination) => destination.Kind switch
    {
        PdfKind.Name => document.Objects.Names.GetString(destination.AsName()),
        PdfKind.String => PdfText.Decode(destination.AsStringBytes()),
        _ => null,
    };
}
