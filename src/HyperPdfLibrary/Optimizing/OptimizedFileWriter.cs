// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Writes an <see cref="OptimizerGraph"/> as a new file one chunk at a time: the header, each plain object, each full
/// object stream and finally the cross-reference section. Each chunk is a pooled array the caller writes and returns,
/// so a file of any size is written with one chunk in memory, synchronously or asynchronously. Streams and the
/// /Encrypt dictionary are written plainly; everything else can be packed into object streams with a cross-reference
/// stream. An encrypted document keeps its security handler and the first part of its /ID, so every string and stream is
/// encrypted again for its new number with the same key.
/// </summary>
[DebuggerDisplay("OptimizedFileWriter: object {_next} of {_graph.Count}")]
internal sealed class OptimizedFileWriter
{
    /// <summary>The most objects packed into one object stream.</summary>
    private const int ObjectsPerStream = 100;

    /// <summary>The new number of the catalog, the first object reached from the trailer.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The objects written between progress reports.</summary>
    private const int ProgressStep = 64;

    /// <summary>The bytes hashed for the second part of /ID: the first part, the size and the object count.</summary>
    private const int IdSeedLength = 32;

    /// <summary>The offset of the file size in the /ID seed.</summary>
    private const int IdSizeOffset = 16;

    /// <summary>The offset of the object count in the /ID seed.</summary>
    private const int IdCountOffset = 24;

    /// <summary>Idle writers, kept so a run does not make one.</summary>
    private readonly ReusableSlots<OptimizedFileWriter> _idle;

    /// <summary>The objects to write.</summary>
    private OptimizerGraph _graph = null!;

    /// <summary>The store they come from.</summary>
    private PdfObjectStore _store = null!;

    /// <summary>The layout.</summary>
    private WriteSettings _settings;

    /// <summary>Changes objects as they are written.</summary>
    private IObjectTransformer _transformer = null!;

    /// <summary>The cross-reference entries, filled in as objects are written; rented from the pool.</summary>
    private XrefRow[]? _rowArray;

    /// <summary>The number of cross-reference entries.</summary>
    private int _rowCount;

    /// <summary>The objects waiting to be packed into the next object stream, by new number; rented from the pool.</summary>
    private int[]? _group;

    /// <summary>The number of objects waiting.</summary>
    private int _groupCount;

    /// <summary>The old number of the /Encrypt dictionary, or zero.</summary>
    private int _encryptNumber;

    /// <summary>1 once the writer has been given back, so a second dispose does not offer it twice.</summary>
    private int _returned;

    /// <summary>The bytes handed out so far.</summary>
    private long _position;

    /// <summary>The next object to write, by new number; zero before the header.</summary>
    private int _next;

    /// <summary>The number of the next object stream.</summary>
    private int _streamNumber;

    /// <summary>Whether the cross-reference section has been handed out.</summary>
    private bool _finished;

    /// <summary>Initializes a new instance of the <see cref="OptimizedFileWriter"/> class.</summary>
    /// <param name="idle">The owning service's writer pool.</param>
    private OptimizedFileWriter(ReusableSlots<OptimizedFileWriter> idle) => _idle = idle;

    /// <summary>Gets the bytes handed out so far.</summary>
    internal long Position => _position;

    /// <summary>Gets the store the objects come from.</summary>
    internal PdfObjectStore Store => _store;

    /// <summary>Gets the registered service's reusable state.</summary>
    internal OptimizerResources Resources => _graph.Resources;

    /// <summary>Takes an idle writer, or makes one, and points it at the objects to write.</summary>
    /// <param name="graph">The objects to write.</param>
    /// <param name="store">The store they come from.</param>
    /// <param name="settings">The layout.</param>
    /// <param name="transformer">Changes objects as they are written.</param>
    /// <returns>The writer, which the caller disposes to hand it back.</returns>
    internal static OptimizedFileWriter Rent(OptimizerGraph graph, PdfObjectStore store, WriteSettings settings, IObjectTransformer transformer)
    {
        var idle = graph.Resources.Writers;
        var writer = idle.Take() ?? new(idle);
        _ = Interlocked.Exchange(ref writer._returned, 0);
        try
        {
            writer.Begin(graph, store, settings, transformer);
        }
        catch
        {
            // The caller still owns the graph when renting the writer fails.
            writer._graph = null!;
            writer.Return();
            throw;
        }

        return writer;
    }

    /// <summary>Gives the writer back to the pool, with its arrays returned.</summary>
    internal void Return()
    {
        if (Interlocked.Exchange(ref _returned, 1) != 0)
        {
            return;
        }

        if (_rowArray is not null)
        {
            ArrayPool<XrefRow>.Shared.Return(_rowArray);
            _rowArray = null;
        }

        if (_group is not null)
        {
            ArrayPool<int>.Shared.Return(_group);
            _group = null;
        }

        _graph?.Return();
        _graph = null!;
        _store = null!;
        _transformer = null!;
        _settings = default;
        _ = _idle.Offer(this);
    }

    /// <summary>Writes the whole file to a stream.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Stops the write between chunks.</param>
    /// <returns>The bytes written.</returns>
    internal long WriteTo(Stream destination, CancellationToken cancellationToken)
    {
        while (NextChunk(cancellationToken, out var length) is { } chunk)
        {
            try
            {
                destination.Write(chunk, 0, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(chunk);
            }
        }

        return _position;
    }

    /// <summary>Writes the whole file to a stream asynchronously.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Stops the write between chunks.</param>
    /// <returns>The bytes written.</returns>
    internal async Task<long> WriteToAsync(Stream destination, CancellationToken cancellationToken)
    {
        while (NextChunk(cancellationToken, out var length) is { } chunk)
        {
            try
            {
                await destination.WriteAsync(chunk.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(chunk);
            }
        }

        return _position;
    }

    /// <summary>Produces the next chunk of the file.</summary>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <param name="length">The chunk's length.</param>
    /// <returns>A pooled array the caller returns to <see cref="ArrayPool{T}.Shared"/>, or <see langword="null"/> at the end.</returns>
    internal byte[]? NextChunk(CancellationToken cancellationToken, out int length)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_next == 0)
        {
            _next = 1;
            return Produce(ChunkKind.Header, out length);
        }

        if (NextObjects(out length) is { } chunk)
        {
            return chunk;
        }

        if (_finished)
        {
            _transformer.Progress(_graph.Count, _graph.Count, _position);
            length = 0;
            return null;
        }

        _finished = true;
        return Produce(ChunkKind.Xref, out length);
    }

    /// <summary>Writes <c>%PDF-x.y</c> and a binary comment.</summary>
    /// <param name="self">The writer.</param>
    /// <param name="writer">The output.</param>
    private static void Header(OptimizedFileWriter self, ref PdfObjectWriter writer)
    {
        writer.WriteRaw("%PDF-"u8);
        foreach (var c in self._settings.Version)
        {
            // The version holds only digits and a dot.
            writer.Buffer.WriteByte((byte)c);
        }

        writer.WriteRaw("\n"u8);
        writer.WriteRaw(PdfXrefWriter.BinaryMarker);
    }

    /// <summary>Writes the cross-reference section and trailer.</summary>
    /// <param name="self">The writer.</param>
    /// <param name="writer">The output.</param>
    private static void Xref(OptimizedFileWriter self, ref PdfObjectWriter writer)
    {
        var rows = self._rowArray.AsSpan(0, self._rowCount);
        var source = self._store.Trailer;
        var keep = self._settings.KeepEncryption;
        var size = rows.Length;
        using var trailerScratch = PdfTrailerScratch.Rent(self._store);
        var trailer = trailerScratch.Dictionary;
        PdfXrefWriter.FillTrailer(trailer, source, size, -1, keep);
        var sectionOffset = self._position;
        Span<byte> seed = stackalloc byte[IdSeedLength];
        var ids = source.GetArray(KnownName.ID);
        var first = ids is null ? [] : ids.Get(0).AsStringBytes();
        first[..Math.Min(first.Length, IdSizeOffset)].CopyTo(seed);
        BinaryPrimitives.WriteInt64LittleEndian(seed[IdSizeOffset..], sectionOffset);
        BinaryPrimitives.WriteInt64LittleEndian(seed[IdCountOffset..], size);
        PdfXrefWriter.SetFileId(trailer, source, seed, keep && self._store.Security is not null);
        if (self._settings.ObjectStreams)
        {
            rows[^1] = new(size - 1, XrefEntryType.InFile, sectionOffset, 0);
            PdfXrefWriter.WriteStream(ref writer, rows, trailer, new(size - 1, 0), sectionOffset);
            return;
        }

        PdfXrefWriter.WriteTable(ref writer, rows);
        PdfXrefWriter.WriteTrailer(ref writer, trailer, sectionOffset);
    }

    /// <summary>Writes the plain object <see cref="_next"/> names, transformed first.</summary>
    /// <param name="self">The writer.</param>
    /// <param name="writer">The output.</param>
    private static void PlainObject(OptimizedFileWriter self, ref PdfObjectWriter writer)
    {
        var number = self._next - 1;
        var oldNumber = self._graph.GetOldNumber(number);
        var value = self._transformer.Transform(oldNumber, self._graph.GetValue(number));
        writer.WriteIndirectObject(new(number, 0), value, oldNumber != self._encryptNumber);
    }

    /// <summary>Writes the waiting objects as one object stream.</summary>
    /// <param name="self">The writer.</param>
    /// <param name="writer">The output.</param>
    private static void ObjectStream(OptimizedFileWriter self, ref PdfObjectWriter writer)
    {
        var body = new PdfObjectWriter(self._store.Names);
        var packer = default(ObjectStreamPacker);
        try
        {
            body.SetRenumbering(self._graph.Map);
            for (var i = 0; i < self._groupCount; i++)
            {
                var number = self._group![i];
                packer.Add(ref body, number, self._graph.GetValue(number));
            }

            packer.Flush(ref body, ref writer, new(self._streamNumber, 0));
        }
        finally
        {
            packer.Dispose();
            body.Dispose();
        }
    }

    /// <summary>Writes objects until one chunk is ready: a plain object or a full object stream.</summary>
    /// <param name="length">The chunk's length.</param>
    /// <returns>The chunk, or <see langword="null"/> when every object has been written.</returns>
    private byte[]? NextObjects(out int length)
    {
        while (_next <= _graph.Count)
        {
            var number = _next;
            _next++;
            ReportProgress(number);
            if (!IsPackable(number))
            {
                _rowArray![number] = new(number, XrefEntryType.InFile, _position, 0);
                return Produce(ChunkKind.Plain, out length);
            }

            _rowArray![number] = new(number, XrefEntryType.Compressed, _streamNumber, _groupCount);
            _group![_groupCount] = number;
            _groupCount++;
            if (_groupCount >= ObjectsPerStream)
            {
                return FlushGroup(out length);
            }
        }

        if (_groupCount > 0)
        {
            return FlushGroup(out length);
        }

        length = 0;
        return null;
    }

    /// <summary>Writes the waiting objects as an object stream and starts the next.</summary>
    /// <param name="length">The chunk's length.</param>
    /// <returns>The chunk.</returns>
    private byte[] FlushGroup(out int length)
    {
        _rowArray![_streamNumber] = new(_streamNumber, XrefEntryType.InFile, _position, 0);
        var chunk = Produce(ChunkKind.ObjectStream, out length);
        _groupCount = 0;
        _streamNumber++;
        return chunk;
    }

    /// <summary>Runs a writing step into a new writer and takes its bytes.</summary>
    /// <param name="step">The step.</param>
    /// <param name="length">The bytes written.</param>
    /// <returns>The pooled array holding them.</returns>
    private byte[] Produce(ChunkKind step, out int length)
    {
        var writer = new PdfObjectWriter(_store.Names, _settings.KeepEncryption ? _store.Security : null);
        try
        {
            writer.SetRenumbering(_graph.Map);
            switch (step)
            {
                case ChunkKind.Header:
                {
                    Header(this, ref writer);
                    break;
                }

                case ChunkKind.Plain:
                {
                    PlainObject(this, ref writer);
                    break;
                }

                case ChunkKind.ObjectStream:
                {
                    ObjectStream(this, ref writer);
                    break;
                }

                default:
                {
                    Xref(this, ref writer);
                    break;
                }
            }

            var chunk = writer.Detach(out length) ?? ArrayPool<byte>.Shared.Rent(1);
            _position += length;
            return chunk;
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Reports progress every few objects.</summary>
    /// <param name="number">The object about to be written.</param>
    private void ReportProgress(int number)
    {
        if (number % ProgressStep == 0)
        {
            _transformer.Progress(number, _graph.Count, _position);
        }
    }

    /// <summary>Points the writer at the objects to write and sizes its tables.</summary>
    /// <param name="graph">The objects to write.</param>
    /// <param name="store">The store they come from.</param>
    /// <param name="settings">The layout.</param>
    /// <param name="transformer">Changes objects as they are written.</param>
    private void Begin(OptimizerGraph graph, PdfObjectStore store, WriteSettings settings, IObjectTransformer transformer)
    {
        _graph = graph;
        _store = store;
        _settings = settings;
        _transformer = transformer;
        _position = 0;
        _next = 0;
        _finished = false;
        _groupCount = 0;
        _encryptNumber = settings.KeepEncryption ? PdfXrefWriter.EncryptNumber(store.Trailer) : 0;
        var streamCount = settings.ObjectStreams ? PackedStreamCount() : 0;
        _rowCount = graph.Count + streamCount + 1 + (settings.ObjectStreams ? 1 : 0);
        _rowArray = ArrayPool<XrefRow>.Shared.Rent(_rowCount);
        Array.Clear(_rowArray, 0, _rowCount);
        _rowArray[0] = XrefRow.FreeHead;
        _group = settings.ObjectStreams ? ArrayPool<int>.Shared.Rent(ObjectsPerStream) : null;
        _streamNumber = graph.Count + 1;
    }

    /// <summary>Determines whether an object can go in an object stream.</summary>
    /// <param name="number">The new number.</param>
    /// <returns><see langword="true"/> when object streams are on and the object is not the catalog, a stream or /Encrypt.</returns>
    private bool IsPackable(int number) =>
        _settings.ObjectStreams && number != CatalogNumber && _graph.GetValue(number).Kind != PdfKind.Stream && _graph.GetOldNumber(number) != _encryptNumber;

    /// <summary>Counts the object streams needed.</summary>
    /// <returns>The number of object streams.</returns>
    private int PackedStreamCount()
    {
        var packed = 0;
        for (var number = 1; number <= _graph.Count; number++)
        {
            packed += IsPackable(number) ? 1 : 0;
        }

        return (packed + ObjectsPerStream - 1) / ObjectsPerStream;
    }
}
