// <copyright file="AudioStreamDetails.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

namespace Drastic.AudioRecorder;

/// <summary>
/// Represents the details of an <see cref="IAudioStream"/>, including channel count, sample rate, and bits per sample.
/// </summary>
public class AudioStreamDetails
{
    /// <summary>
    /// Gets or sets the sample rate of the underlying audio stream.
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Gets or sets the channel count of the underlying audio stream.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the bits per sample of the underlying audio stream.
    /// </summary>
    public int BitsPerSample { get; set; }
}