// <copyright file="IAudioStream.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

namespace Drastic.AudioRecorder;

public interface IAudioStream
{
    /// <summary>
    /// Occurs when new audio has been streamed.
    /// </summary>
    event EventHandler<byte[]> OnBroadcast;

    /// <summary>
    /// Occurs when the audio stream active status changes.
    /// </summary>
    event EventHandler<bool> OnActiveChanged;

    /// <summary>
    /// Occurs when there's an error while capturing audio.
    /// </summary>
    event EventHandler<Exception> OnException;

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    /// <value>
    /// The sample rate.
    /// </value>
    int SampleRate { get; }

    /// <summary>
    /// Gets the channel count.
    /// </summary>
    /// <value>
    /// The channel count.
    /// </value>
    int ChannelCount { get; }

    /// <summary>
    /// Gets bits per sample.
    /// </summary>
    int BitsPerSample { get; }

    /// <summary>
    /// Gets a value indicating whether gets a value indicating if the audio stream is active.
    /// </summary>
    bool Active { get; }

    /// <summary>
    /// Starts the audio stream.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task Start();

    /// <summary>
    /// Stops the audio stream.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task Stop();

    /// <summary>
    /// Flushes any audio bytes in memory but not yet broadcast out to any listeners.
    /// </summary>
    void Flush();
}