// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.MacOS;

/// <summary>Source generated entry points of the macOS system libraries and frameworks the integration uses.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The Objective-C runtime.</summary>
    internal const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    /// <summary>Core Foundation.</summary>
    internal const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>Audio Toolbox, home of the CoreAudio audio queues.</summary>
    internal const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";

    /// <summary>Native <c>objc_getClass</c>.</summary>
    /// <param name="name">The UTF-8 class name.</param>
    /// <returns>The class.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint GetClass(string name);

    /// <summary>Native <c>sel_registerName</c>.</summary>
    /// <param name="name">The UTF-8 selector.</param>
    /// <returns>The selector.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint Selector(string name);

    /// <summary>Native <c>objc_msgSend</c> with no arguments, returning an object.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint Send(nint receiver, nint selector);

    /// <summary>Native <c>objc_msgSend</c> with one object argument, returning an object.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint Send(nint receiver, nint selector, nint argument);

    /// <summary>Native <c>objc_msgSend</c> returning an unsigned integer, such as <c>count</c>.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nuint SendCount(nint receiver, nint selector);

    /// <summary>Native <c>objc_msgSend</c> taking an unsigned integer, such as <c>objectAtIndex:</c>.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="index">The index.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendIndex(nint receiver, nint selector, nuint index);

    /// <summary>Native <c>objc_msgSend</c> for <c>checkSpellingOfString:startingAt:</c>, which returns an NSRange in registers.</summary>
    /// <param name="receiver">The spell checker.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="text">The NSString.</param>
    /// <param name="start">Where to start.</param>
    /// <returns>The first misspelled word, or a location of NSNotFound.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial CoreFoundationRange SendCheckSpelling(nint receiver, nint selector, nint text, nint start);

    /// <summary>Native <c>objc_msgSend</c> for <c>guessesForWordRange:inString:language:inSpellDocumentWithTag:</c>.</summary>
    /// <param name="receiver">The spell checker.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="range">The word's range in the string.</param>
    /// <param name="text">The NSString.</param>
    /// <param name="language">The language, or null for the reader's.</param>
    /// <param name="tag">The spell document tag, 0 for none.</param>
    /// <returns>An autoreleased NSArray of NSString guesses, or null.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendGuesses(nint receiver, nint selector, CoreFoundationRange range, nint text, nint language, nint tag);

    /// <summary>Native <c>objc_autoreleasePoolPush</c>.</summary>
    /// <returns>The pool.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPush")]
    internal static partial nint AutoreleasePoolPush();

    /// <summary>Native <c>objc_autoreleasePoolPop</c>.</summary>
    /// <param name="pool">The pool.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPop")]
    internal static partial void AutoreleasePoolPop(nint pool);

    /// <summary>Native <c>CFStringCreateWithCharacters</c>.</summary>
    /// <param name="allocator">The allocator, or null.</param>
    /// <param name="characters">The UTF-16 characters.</param>
    /// <param name="length">The character count.</param>
    /// <returns>The string, released with <see cref="Release"/>.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFStringCreateWithCharacters")]
    internal static partial nint CreateString(nint allocator, char* characters, long length);

    /// <summary>Native <c>CFStringGetLength</c>.</summary>
    /// <param name="value">The string.</param>
    /// <returns>The length in UTF-16 units.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFStringGetLength")]
    internal static partial long GetStringLength(nint value);

    /// <summary>Native <c>CFStringGetCharacters</c>.</summary>
    /// <param name="value">The string.</param>
    /// <param name="range">The range to copy.</param>
    /// <param name="buffer">Receives the characters.</param>
    [LibraryImport(CoreFoundation, EntryPoint = "CFStringGetCharacters")]
    internal static partial void GetCharacters(nint value, CoreFoundationRange range, char* buffer);

    /// <summary>Native <c>CFRelease</c>.</summary>
    /// <param name="value">The object.</param>
    [LibraryImport(CoreFoundation, EntryPoint = "CFRelease")]
    internal static partial void Release(nint value);

    /// <summary>Native <c>CFGetTypeID</c>.</summary>
    /// <param name="value">The object.</param>
    /// <returns>Its type.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFGetTypeID")]
    internal static partial nuint GetTypeId(nint value);

    /// <summary>Native <c>CFStringGetTypeID</c>.</summary>
    /// <returns>The string type.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFStringGetTypeID")]
    internal static partial nuint StringTypeId();

    /// <summary>Native <c>CFNumberGetTypeID</c>.</summary>
    /// <returns>The number type.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFNumberGetTypeID")]
    internal static partial nuint NumberTypeId();

    /// <summary>Native <c>CFBooleanGetTypeID</c>.</summary>
    /// <returns>The boolean type.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFBooleanGetTypeID")]
    internal static partial nuint BooleanTypeId();

    /// <summary>Native <c>CFNumberGetValue</c>.</summary>
    /// <param name="number">The number.</param>
    /// <param name="type">The wanted type.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns>Nonzero when exact.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFNumberGetValue")]
    internal static partial byte GetNumberValue(nint number, nint type, long* value);

    /// <summary>Native <c>CFBooleanGetValue</c>.</summary>
    /// <param name="boolean">The boolean.</param>
    /// <returns>Nonzero for true.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFBooleanGetValue")]
    internal static partial byte GetBooleanValue(nint boolean);

    /// <summary>Native <c>CFPreferencesCopyAppValue</c>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="application">The preferences domain.</param>
    /// <returns>The value, released with <see cref="Release"/>, or null.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFPreferencesCopyAppValue")]
    internal static partial nint CopyPreference(nint key, nint application);

    /// <summary>Native <c>CFPreferencesAppSynchronize</c>.</summary>
    /// <param name="application">The preferences domain.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFPreferencesAppSynchronize")]
    internal static partial byte SynchronizePreferences(nint application);

    /// <summary>Native <c>CFNotificationCenterGetDistributedCenter</c>.</summary>
    /// <returns>The distributed notification centre.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFNotificationCenterGetDistributedCenter")]
    internal static partial nint GetDistributedCenter();

    /// <summary>Native <c>CFNotificationCenterAddObserver</c>.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="observer">The observer's identity.</param>
    /// <param name="callback">The callback.</param>
    /// <param name="name">The notification name.</param>
    /// <param name="sender">The sender, or null for any.</param>
    /// <param name="suspension">The delivery behaviour.</param>
    [LibraryImport(CoreFoundation, EntryPoint = "CFNotificationCenterAddObserver")]
    internal static partial void AddObserver(nint center, nint observer, delegate* unmanaged<nint, nint, nint, nint, nint, void> callback, nint name, nint sender, nint suspension);

    /// <summary>Native <c>CFNotificationCenterRemoveEveryObserver</c>.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="observer">The observer's identity.</param>
    [LibraryImport(CoreFoundation, EntryPoint = "CFNotificationCenterRemoveEveryObserver")]
    internal static partial void RemoveEveryObserver(nint center, nint observer);

    /// <summary>Native <c>AudioQueueNewOutput</c>.</summary>
    /// <param name="format">The sample format.</param>
    /// <param name="callback">Called as each buffer finishes.</param>
    /// <param name="userData">Passed to the callback.</param>
    /// <param name="runLoop">The run loop for callbacks, or null for an internal thread.</param>
    /// <param name="runLoopMode">The run loop mode, or null.</param>
    /// <param name="flags">Reserved, zero.</param>
    /// <param name="queue">Receives the queue.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(AudioToolbox, EntryPoint = "AudioQueueNewOutput")]
    internal static partial int NewOutputQueue(StreamDescription* format, delegate* unmanaged<nint, nint, nint, void> callback, nint userData, nint runLoop, nint runLoopMode, uint flags, nint* queue);

    /// <summary>Native <c>AudioQueueAllocateBuffer</c>.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="size">The buffer size in bytes.</param>
    /// <param name="buffer">Receives the buffer.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(AudioToolbox, EntryPoint = "AudioQueueAllocateBuffer")]
    internal static partial int AllocateBuffer(nint queue, uint size, nint* buffer);

    /// <summary>Native <c>AudioQueueEnqueueBuffer</c>.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="descriptionCount">Packet descriptions, zero for PCM.</param>
    /// <param name="descriptions">The descriptions, null.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(AudioToolbox, EntryPoint = "AudioQueueEnqueueBuffer")]
    internal static partial int EnqueueBuffer(nint queue, nint buffer, uint descriptionCount, nint descriptions);

    /// <summary>Native <c>AudioQueueStart</c>.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="startTime">When to start, null for now.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(AudioToolbox, EntryPoint = "AudioQueueStart")]
    internal static partial int StartQueue(nint queue, nint startTime);

    /// <summary>Native <c>AudioQueueFlush</c>.</summary>
    /// <param name="queue">The queue.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(AudioToolbox, EntryPoint = "AudioQueueFlush")]
    internal static partial int FlushQueue(nint queue);

    /// <summary>Native <c>AudioQueueStop</c>.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="immediate">Nonzero to stop now; zero to stop once the queued audio has played.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(AudioToolbox, EntryPoint = "AudioQueueStop")]
    internal static partial int StopQueue(nint queue, byte immediate);

    /// <summary>Native <c>AudioQueueDispose</c>.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="immediate">Nonzero to dispose now.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(AudioToolbox, EntryPoint = "AudioQueueDispose")]
    internal static partial int DisposeQueue(nint queue, byte immediate);

    /// <summary>Native <c>AudioQueueGetProperty</c>.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="property">The property.</param>
    /// <param name="value">Receives the value.</param>
    /// <param name="size">The value's size; receives the size written.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(AudioToolbox, EntryPoint = "AudioQueueGetProperty")]
    internal static partial int GetQueueProperty(nint queue, uint property, void* value, uint* size);
}
