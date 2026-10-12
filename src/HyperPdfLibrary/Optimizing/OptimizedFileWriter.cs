// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdfLibrary.Objects;
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

    /// <summary>The objects to write.</summary>
    private readonly OptimizerGraph _graph;

    /// <summary>The store they come from.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The layout.</summary>
    private readonly WriteSettings _settings;

    /// <summary>Changes objects as they are written.</summary>
    private readonly IObjectTransformer _transformer;

    /// <summary>The cross-reference entries, filled in as objects are written.</summary>
    private readonly XrefRow[] _rows;

    /// <summary>The objects waiting to be packed into the next object stream, by new number.</summary>
    private readonly List<int> _group = [with(ObjectsPerStream)];

    /// <summary>The old number of the /Encrypt dictionary, or zero.</summary>
    private readonly int _encryptNumber;

    /// <summary>The bytes handed out so far.</summary>
    private long _position;

    /// <summary>The next object to write, by new number; zero before the header.</summary>
    private int _next;

    /// <summary>The number of the next object stream.</summary>
    private int _streamNumber;

    /// <summary>Whether the cross-reference section has been handed out.</summary>
    private bool _finished;

    /// <summary>Initializes a new instance of the <see cref="OptimizedFileWriter"/> class.</summary>
    /// <param name="graph">The objects to write.</param>
    /// <param name="store">The store they come from.</param>
    /// <param name="settings">The layout.</param>
    /// <param name="transformer">Changes objects as they are written.</param>
    internal OptimizedFileWriter(OptimizerGraph graph, PdfObjectStore store, WriteSettings settings, IObjectTransformer transformer)
    {
        _graph = graph;
        _store = store;
        _settings = settings;
        _transformer = transformer;
        _encryptNumber = settings.KeepEncryption ? PdfXrefWriter.EncryptNumber(store.Trailer) : 0;
        var streamCount = settings.ObjectStreams ? PackedStreamCount() : 0;
        _rows = new XrefRow[graph.Count + streamCount + 1 + (settings.ObjectStreams ? 1 : 0)];
        _rows[0] = XrefRow.FreeHead;
        _streamNumber = graph.Count + 1;
    }

    /// <summary>Gets the bytes handed out so far.</summary>
    internal long Position => _position;

    /// <summary>Gets the store the objects come from.</summary>
    internal PdfObjectStore Store => _store;

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
        var rows = self._rows;
        var source = self._store.Trailer;
        var keep = self._settings.KeepEncryption;
        var size = rows.Length;
        var trailer = PdfXrefWriter.CreateTrailer(source, size, -1, keep);
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
            foreach (var number in self._group)
            {
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
                _rows[number] = new(number, XrefEntryType.InFile, _position, 0);
                return Produce(ChunkKind.Plain, out length);
            }

            _rows[number] = new(number, XrefEntryType.Compressed, _streamNumber, _group.Count);
            _group.Add(number);
            if (_group.Count >= ObjectsPerStream)
            {
                return FlushGroup(out length);
            }
        }

        if (_group.Count > 0)
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
        _rows[_streamNumber] = new(_streamNumber, XrefEntryType.InFile, _position, 0);
        var chunk = Produce(ChunkKind.ObjectStream, out length);
        _group.Clear();
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
