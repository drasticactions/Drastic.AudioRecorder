// <copyright file="AudioRecorderService.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Drastic.AudioRecorder;

/// <summary>
/// A service that records audio on the device's microphone input.
/// </summary>
public partial class AudioRecorderService
{
    private const string DefaultFileName = "ARS_recording.wav";
    private const float NearZero = .00000000001F;

    private WaveRecorder recorder;

    private IAudioStream audioStream;

    private bool audioDetected;
    private DateTime? silenceTime;
    private DateTime? startTime;
    private TaskCompletionSource<string> recordTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioRecorderService"/> class.
    /// Creates a new instance of the <see cref="AudioRecorderService"/>.
    /// </summary>
    public AudioRecorderService()
    {
        this.Init();
    }

    /// <summary>
    /// This event is raised when audio recording is complete and delivers a full filepath to the recorded audio file.
    /// </summary>
    /// <remarks>This event will be raised on a background thread to allow for any further processing needed.  The audio file will be <c>null</c> in the case that no audio was recorded.</remarks>
    public event EventHandler<string> AudioInputReceived;

    /// <summary>
    /// Gets the details of the underlying audio stream.
    /// </summary>
    /// <remarks>Accessible once <see cref="StartRecording"/> has been called.</remarks>
    public AudioStreamDetails AudioStreamDetails { get; private set; }

    /// <summary>
    /// Gets or sets /sets the desired file path. If null it will be set automatically
    /// to a temporary file.
    /// </summary>
    public string FilePath { get; set; }

    /// <summary>
    /// Gets or sets /sets the preferred sample rate to be used during recording.
    /// </summary>
    /// <remarks>This value may be overridden by platform-specific implementations, e.g. the Android AudioManager will be asked for its preferred sample rate and may override any user-set value here.</remarks>
    public int PreferredSampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets a value indicating whether returns a value indicating if the <see cref="AudioRecorderService"/> is currently recording audio.
    /// </summary>
    public bool IsRecording => this.audioStream?.Active ?? false;

    /// <summary>
    /// Gets or sets if <see cref="StopRecordingOnSilence"/> is set to <c>true</c>, this <see cref="TimeSpan"/> indicates the amount of 'silent' time is required before recording is stopped.
    /// </summary>
    /// <remarks>Defaults to 2 seconds.</remarks>
    public TimeSpan AudioSilenceTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets if <see cref="StopRecordingAfterTimeout"/> is set to <c>true</c>, this <see cref="TimeSpan"/> indicates the total amount of time to record audio for before recording is stopped. Defaults to 30 seconds.
    /// </summary>
    /// <seealso cref="StopRecordingAfterTimeout"/>
    public TimeSpan TotalAudioTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether gets/sets a value indicating if the <see cref="AudioRecorderService"/> should stop recording after silence (low audio signal) is detected.
    /// </summary>
    /// <remarks>Default is `true`.</remarks>
    public bool StopRecordingOnSilence { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether gets/sets a value indicating if the <see cref="AudioRecorderService"/> should stop recording after a certain amount of time.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    /// <seealso cref="TotalAudioTimeout"/>
    public bool StopRecordingAfterTimeout { get; set; } = true;

    /// <summary>
    /// Gets or sets /sets a value indicating the signal threshold that determines silence.  If the recorder is being over or under aggressive when detecting silence, you can alter this value to achieve different results.
    /// </summary>
    /// <remarks>Defaults to .15.  Value should be between 0 and 1.</remarks>
    public float SilenceThreshold { get; set; } = .15f;

    /// <summary>
    /// Starts recording audio.
    /// </summary>
    /// <returns>A <see cref="Task"/> that will complete when recording is finished.
    /// The task result will be the path to the recorded audio file, or null if no audio was recorded.</returns>
    public async Task<Task<string>> StartRecording()
    {
        if (this.FilePath == null)
        {
            this.FilePath = await this.GetDefaultFilePath();
        }

        this.ResetAudioDetection();
        this.OnRecordingStarting();

        this.InitializeStream(this.PreferredSampleRate);

        await this.recorder.StartRecorder(this.audioStream, this.FilePath);

        this.AudioStreamDetails = new AudioStreamDetails
        {
            ChannelCount = this.audioStream.ChannelCount,
            SampleRate = this.audioStream.SampleRate,
            BitsPerSample = this.audioStream.BitsPerSample,
        };

        this.startTime = DateTime.Now;
        this.recordTask = new TaskCompletionSource<string>();

        Debug.WriteLine("AudioRecorderService.StartRecording() complete.  Audio is being recorded.");

        return this.recordTask.Task;
    }

    partial void Init();

    /// <summary>
    /// Gets a new <see cref="Stream"/> to the recording audio file in readonly mode.
    /// </summary>
    /// <returns>A <see cref="Stream"/> object that can be used to read the audio file from the beginning.</returns>
    public Stream GetAudioFileStream()
    {
        return this.recorder.GetAudioFileStream();
    }

    /// <summary>
    /// Stops recording audio.
    /// </summary>
    /// <param name="continueProcessing"><c>true</c> (default) to finish recording and raise the <see cref="AudioInputReceived"/> event.
    /// Use <c>false</c> here to stop recording but do nothing further (from an error state, etc.).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task StopRecording(bool continueProcessing = true)
    {
        this.audioStream.Flush(); // allow the stream to send any remaining data
        this.audioStream.OnBroadcast -= this.AudioStream_OnBroadcast;

        try
        {
            await this.audioStream.Stop();

            // WaveRecorder will be stopped as result of stream stopping
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in StopRecording: {0}", ex);
        }

        this.OnRecordingStopped();

        var returnedFilePath = this.GetAudioFilePath();

        // complete the recording Task for anthing waiting on this
        this.recordTask.TrySetResult(returnedFilePath);

        if (continueProcessing)
        {
            Debug.WriteLine($"AudioRecorderService.StopRecording(): Recording stopped, raising AudioInputReceived event; audioDetected == {this.audioDetected}; filePath == {returnedFilePath}");

            this.AudioInputReceived?.Invoke(this, returnedFilePath);
        }
    }

    /// <summary>
    /// Gets the full filepath to the recorded audio file.
    /// </summary>
    /// <returns>The full filepath to the recorded audio file, or null if no audio was detected during the last record.</returns>
    public string GetAudioFilePath()
    {
        return this.audioDetected ? this.FilePath : null;
    }

    private void ResetAudioDetection()
    {
        this.audioDetected = false;
        this.silenceTime = null;
        this.startTime = null;
    }

    private void AudioStream_OnBroadcast(object sender, byte[] bytes)
    {
        var level = AudioFunctions.CalculateLevel(bytes);

        if (level < NearZero && !this.audioDetected) // discard any initial 0s so we don't jump the gun on timing out
        {
            Debug.WriteLine("level == {0} && !audioDetected", level);
            return;
        }

        if (level > this.SilenceThreshold) // did we find a signal?
        {
            this.audioDetected = true;
            this.silenceTime = null;

            Debug.WriteLine("AudioStream_OnBroadcast :: {0} :: level > SilenceThreshold :: bytes: {1}; level: {2}", DateTime.Now, bytes.Length, level);
        }
        else // no audio detected
        {
            // see if we've detected 'near' silence for more than <audioTimeout>
            if (this.StopRecordingOnSilence && this.silenceTime.HasValue)
            {
                var currentTime = DateTime.Now;

                if (currentTime.Subtract(this.silenceTime.Value).TotalMilliseconds > this.AudioSilenceTimeout.TotalMilliseconds)
                {
                    this.Timeout($"AudioStream_OnBroadcast :: {currentTime} :: AudioSilenceTimeout exceeded, stopping recording :: Near-silence detected at: {this.silenceTime}");
                    return;
                }
            }
            else
            {
                this.silenceTime = DateTime.Now;

                Debug.WriteLine("AudioStream_OnBroadcast :: {0} :: Near-silence detected :: bytes: {1}; level: {2}", this.silenceTime, bytes.Length, level);
            }
        }

        if (this.StopRecordingAfterTimeout && DateTime.Now - this.startTime > this.TotalAudioTimeout)
        {
            this.Timeout("AudioStream_OnBroadcast(): TotalAudioTimeout exceeded, stopping recording");
        }
    }

    private void Timeout(string reason)
    {
        Debug.WriteLine(reason);
        this.audioStream.OnBroadcast -= this.AudioStream_OnBroadcast; // need this to be immediate or we can try to stop more than once

        // since we're in the middle of handling a broadcast event when an audio timeout occurs, we need to break the StopRecording call on another thread
        // Otherwise, Bad. Things. Happen.
        _ = Task.Run(() => this.StopRecording());
    }

    private void InitializeStream(int sampleRate)
    {
        try
        {
            if (this.audioStream != null)
            {
                this.audioStream.OnBroadcast -= this.AudioStream_OnBroadcast;
            }
            else
            {
                this.audioStream = this.GenerateAudioStream(sampleRate);
            }

            this.audioStream.OnBroadcast += this.AudioStream_OnBroadcast;

            if (this.recorder == null)
            {
                this.recorder = new WaveRecorder();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error: {0}", ex);
        }
    }

#if NETSTANDARD

    private IAudioStream GenerateAudioStream(int sampleRate)
    {
        throw new NotImplementedException();
    }

    private Task<string> GetDefaultFilePath()
    {
        return Task.FromResult(Path.Combine(Path.GetTempPath(), DefaultFileName));
    }

    private void OnRecordingStarting()
    {
    }

    private void OnRecordingStopped()
    {
    }
#endif
}