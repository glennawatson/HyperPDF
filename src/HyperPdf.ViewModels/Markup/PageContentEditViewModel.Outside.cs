// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The command of Edit mode that sends a picked picture to another program and draws it again each time it is saved there.</summary>
public sealed partial class PageContentEditViewModel
{
    /// <summary>How long the picture's file is watched for a save in another program.</summary>
    private static readonly TimeSpan WatchLimit = TimeSpan.FromMinutes(30);

    /// <summary>How often the picture's file is looked at.</summary>
    private static readonly TimeSpan WatchPeriod = TimeSpan.FromSeconds(1);

    /// <summary>The clock the watch limit is measured with.</summary>
    private readonly TimeProvider _outsideClock = TimeProvider.System;

    /// <summary>Gets the interaction that opens a file in the program the system uses for it; the output is whether a program accepted it.</summary>
    public Interaction<string, bool> ExternalEditInteraction { get; } = new();

    /// <summary>Writes a picture to a new file in its own folder in the temporary folder.</summary>
    /// <param name="file">The picture.</param>
    /// <returns>The path of the file.</returns>
    private static string WriteOutsideCopy(PageObjectFile file)
    {
        var folder = Path.Combine(Path.GetTempPath(), "HyperPDF", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"picture.{file.Extension}"));
        File.WriteAllBytes(path, file.Data);
        return path;
    }

    /// <summary>Reads a file that another program may still be writing.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The bytes, or <see langword="null"/> when the file could not be read yet.</returns>
    private static byte[]? TryRead(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return bytes.Length == 0 ? null : bytes;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Reads the file on the pool.</summary>
    /// <param name="path">The file.</param>
    /// <returns>A cold signal giving the bytes, or <see langword="null"/> while another program still has the file.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<byte[]?> ReadOutside(string path) => Signal.Start(() => TryRead(path), TaskPoolSequencer.Default);

    /// <summary>Writes the picked picture to a file, opens it in the program the system uses for it, and draws it again each time it is saved there.</summary>
    /// <returns>A signal that ends when the watching ends: after half an hour, when the tab closes, or when the picture cannot be changed.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> EditPickedPictureOutside()
    {
        if (!CanShape(out var picked) || picked.Object.Kind != PageObjectKind.Image || Editor is not { } editor)
        {
            Status = UiText.Get("Pick a picture to change in another program first.");
            return Signal.Return(RxVoid.Default);
        }

        return editor.ExtractPicture(picked.Page, picked.Object.Index)
            .TakeUntil(_lifetime)
            .SubscribeOn(TaskPoolSequencer.Default)
            .SwitchMap(static file => Signal.Start(() => WriteOutsideCopy(file), TaskPoolSequencer.Default))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(path => OpenOutside(picked, path))
            .Catch<RxVoid, Exception>(SaveFailed);
    }

    /// <summary>Asks the view to open the file, and starts watching it when it opened.</summary>
    /// <param name="picked">The picked picture.</param>
    /// <param name="path">The file.</param>
    /// <returns>A signal that ends when the watching ends.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> OpenOutside(PageObjectPick picked, string path) =>
        ExternalEditInteraction.Handle(path).ToSignal().SwitchMap(opened => opened ? StartWatching(picked, path) : NoProgram(path));

    /// <summary>Starts watching a file that was opened in another program.</summary>
    /// <param name="picked">The picked picture.</param>
    /// <param name="path">The file.</param>
    /// <returns>A signal that ends when the watching ends.</returns>
    private IObservable<RxVoid> StartWatching(PageObjectPick picked, string path)
    {
        Status = UiText.Get("Change the picture in the other program and save it there. It changes here when you do.");
        return WatchOutside(picked, path, File.GetLastWriteTimeUtc(path), _outsideClock.GetUtcNow().UtcDateTime + WatchLimit);
    }

    /// <summary>Says no program opened the picture's file, and where the file is.</summary>
    /// <param name="path">The file.</param>
    /// <returns>A signal that ends at once.</returns>
    private IObservable<RxVoid> NoProgram(string path)
    {
        _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("No program could open the picture. It is saved here:")} {path}"));
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Waits a moment, then draws the picture again if the file was saved since it was last read.</summary>
    /// <param name="picked">The picked picture.</param>
    /// <param name="path">The file.</param>
    /// <param name="seen">The time the file was last saved, as far as it has been read.</param>
    /// <param name="until">When to stop watching.</param>
    /// <returns>A signal that ends when the watching ends.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> WatchOutside(PageObjectPick picked, string path, DateTime seen, DateTime until) =>
        Signal.Timer(WatchPeriod, RxSchedulers.MainThreadScheduler)
            .TakeUntil(_lifetime)
            .SwitchMap(_ => LookAtFile(picked, path, seen, until));

    /// <summary>Looks at the file once and carries on watching, drawing the picture again first when the file was saved.</summary>
    /// <param name="picked">The picked picture.</param>
    /// <param name="path">The file.</param>
    /// <param name="seen">The time the file was last saved, as far as it has been read.</param>
    /// <param name="until">When to stop watching.</param>
    /// <returns>A signal that ends when the watching ends.</returns>
    private IObservable<RxVoid> LookAtFile(PageObjectPick picked, string path, DateTime seen, DateTime until)
    {
        if (_outsideClock.GetUtcNow().UtcDateTime >= until || !File.Exists(path))
        {
            return Signal.Return(RxVoid.Default);
        }

        var saved = File.GetLastWriteTimeUtc(path);
        if (saved == seen)
        {
            return WatchOutside(picked, path, seen, until);
        }

        return ReadOutside(path)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(bytes => bytes is null
                ? WatchOutside(picked, path, seen, until)
                : ReplaceOutside(picked, bytes).SwitchMap(_ => WatchOutside(picked, path, saved, until)));
    }

    /// <summary>Draws the picture the other program saved in place of the picked picture.</summary>
    /// <param name="picked">The picked picture.</param>
    /// <param name="bytes">The file's bytes.</param>
    /// <returns>A cold signal that ends when the page has changed; a picture that cannot be read is told to the reader and ends it too.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> ReplaceOutside(PageObjectPick picked, byte[] bytes) =>
        Edit(picked, _ => Editor!.ReplacePicture(picked.Page, picked.Object.Index, bytes), picked.Object.Bounds, UiText.Get("Changed the picture from the other program."))
            .Catch<RxVoid, InvalidDataException>(UnreadableOutside);

    /// <summary>Says the picture the other program saved could not be read.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends at once.</returns>
    private IObservable<RxVoid> UnreadableOutside(InvalidDataException error)
    {
        _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("That picture could not be read.")} {error.Message}"));
        return Signal.Return(RxVoid.Default);
    }
}
