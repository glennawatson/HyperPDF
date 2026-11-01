// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Markup;

namespace HyperPdf.Benchmarks;

/// <summary>Measures finding the object under the pointer in Edit mode, which runs each time the pointer moves.</summary>
public class PageObjectHitsBenchmarks
{
    /// <summary>The objects on a busy page.</summary>
    private const int ObjectCount = 500;

    /// <summary>The columns the objects are laid out in.</summary>
    private const int Columns = 20;

    /// <summary>The width of each cell in points.</summary>
    private const float CellWidth = 28;

    /// <summary>The height of each cell in points.</summary>
    private const float CellHeight = 12;

    /// <summary>The gap left at the right and foot of each cell, in points.</summary>
    private const float Gap = 2;

    /// <summary>How far inside a cell the probe point is, in points.</summary>
    private const float Inset = 1;

    /// <summary>A coordinate left of and above the page, where no object is.</summary>
    private const float Outside = -50;

    /// <summary>The objects of the page, in drawing order.</summary>
    private readonly PageObjectInfo[] _objects = CreateObjects();

    /// <summary>The point in the last object, which is the slowest to find.</summary>
    private readonly PagePoint _last = Probe(ObjectCount - 1);

    /// <summary>Finds the object at a point in the last cell.</summary>
    /// <returns>The index of the object.</returns>
    [Benchmark]
    public int FindLast() => PageObjectHits.Find(_objects, _last);

    /// <summary>Finds the object at a point outside every object, which checks them all.</summary>
    /// <returns>The index of the object, which is -1.</returns>
    [Benchmark]
    public int FindNothing() => PageObjectHits.Find(_objects, new(Outside, Outside));

    /// <summary>Gets a point just inside a cell.</summary>
    /// <param name="index">The cell's number.</param>
    /// <returns>The point.</returns>
    private static PagePoint Probe(int index)
    {
        var row = Math.DivRem(index, Columns, out var column);
        return new((column * CellWidth) + Inset, (row * CellHeight) + Inset);
    }

    /// <summary>Lays out the objects of a busy page in rows.</summary>
    /// <returns>The objects.</returns>
    private static PageObjectInfo[] CreateObjects()
    {
        var objects = new PageObjectInfo[ObjectCount];
        for (var i = 0; i < objects.Length; i++)
        {
            var corner = Probe(i);
            objects[i] = new(i, PageObjectKind.Text, new(corner.X - Inset, corner.Y - Inset, CellWidth - Gap, CellHeight - Gap), "word");
        }

        return objects;
    }
}
