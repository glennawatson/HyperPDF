// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Follows the references of values to collect the objects a section of a linearized file needs. The walk keeps its own
/// stack, so a long chain of references, such as a linked list of outline items or structure elements, needs no call
/// depth; the set of reached objects ends cycles.
/// </summary>
internal ref struct PdfLinearizationWalk : IDisposable
{
    /// <summary>The repaired objects.</summary>
    private readonly PdfObjectGraph _graph;

    /// <summary>The token that cancels the walk.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The graph numbers outside every walk: the catalog, the page tree, the pages and the sections already placed.</summary>
    private PooledBits _excluded;

    /// <summary>The graph numbers reached by the current walk.</summary>
    private PooledBits _reached;

    /// <summary>The values still to visit; empty between scans.</summary>
    private PooledValueStack _pending;

    /// <summary>Whether the current walk follows form parent and child links.</summary>
    private bool _includeFormTree;

    /// <summary>Initializes a new instance of the <see cref="PdfLinearizationWalk"/> struct.</summary>
    /// <param name="graph">The repaired objects.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal PdfLinearizationWalk(PdfObjectGraph graph, CancellationToken cancellationToken)
    {
        _graph = graph;
        _cancellationToken = cancellationToken;
        _excluded = default;
        _reached = default;
        _pending = default;
        _excluded.Rent(graph.Count + 1);
        _reached.Rent(graph.Count + 1);
        _includeFormTree = false;
    }

    /// <summary>Gets the graph numbers the current walk reached.</summary>
    internal readonly PooledBits Reached => _reached;

    /// <summary>Returns the rented memory.</summary>
    public void Dispose()
    {
        _excluded.Dispose();
        _reached.Dispose();
        _pending.Dispose();
    }

    /// <summary>Starts a walk with nothing reached.</summary>
    /// <param name="includeFormTree">Whether to follow form parent and child links.</param>
    internal void Begin(bool includeFormTree)
    {
        _reached.Clear();
        _includeFormTree = includeFormTree;
    }

    /// <summary>Starts a walk that has already reached an object.</summary>
    /// <param name="includeFormTree">Whether to follow form parent and child links.</param>
    /// <param name="graphNumber">The object already reached.</param>
    internal void Begin(bool includeFormTree, int graphNumber)
    {
        Begin(includeFormTree);
        _ = _reached.Add(graphNumber);
    }

    /// <summary>Leaves an object out of every later walk.</summary>
    /// <param name="graphNumber">The graph number.</param>
    internal readonly void Exclude(int graphNumber) => _ = _excluded.Add(graphNumber);

    /// <summary>Leaves objects out of every later walk.</summary>
    /// <param name="graphNumbers">The graph numbers.</param>
    internal readonly void Exclude(ReadOnlySpan<int> graphNumbers)
    {
        foreach (var graphNumber in graphNumbers)
        {
            _ = _excluded.Add(graphNumber);
        }
    }

    /// <summary>Collects the indirect objects a value reaches.</summary>
    /// <param name="value">The value.</param>
    internal void Scan(PdfValue value)
    {
        _pending.Push(value);
        while (_pending.TryPop(out var next))
        {
            switch (next.Kind)
            {
                case PdfKind.Reference:
                    {
                        PushReference(next.AsReference().Number);
                        break;
                    }

                case PdfKind.Array:
                    {
                        foreach (var item in next.AsArray()!.Items)
                        {
                            _pending.Push(item);
                        }

                        break;
                    }

                case PdfKind.Dictionary or PdfKind.Stream:
                    {
                        PushDictionary(next.AsDictionary()!, next.Kind == PdfKind.Stream);
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
        }
    }

    /// <summary>Queues one indirect dependency when it is not excluded and not yet reached.</summary>
    /// <param name="number">The old object number.</param>
    private void PushReference(int number)
    {
        if (number < 1 || number >= _graph.Map.Length)
        {
            return;
        }

        var graphNumber = _graph.Map[number];
        if (graphNumber < 1 || _excluded.Contains(graphNumber) || !_reached.Add(graphNumber))
        {
            return;
        }

        _cancellationToken.ThrowIfCancellationRequested();
        _pending.Push(_graph.GetValue(graphNumber));
    }

    /// <summary>Queues dictionary entries other than page parents, form children and stream lengths.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="isStream">Whether the writer replaces its length.</param>
    private void PushDictionary(PdfDictionary dictionary, bool isStream)
    {
        for (var index = 0; index < dictionary.Count; index++)
        {
            var key = dictionary.GetKeyAt(index);
            if ((!_includeFormTree && (key.Is(KnownName.Parent) || key.Is(KnownName.Kids))) || (isStream && key.Is(KnownName.Length)))
            {
                continue;
            }

            _pending.Push(dictionary.GetValueAt(index));
        }
    }
}
