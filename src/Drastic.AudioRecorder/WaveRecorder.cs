// <copyright file="WaveRecorder.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;

namespace Drastic.AudioRecorder;

internal class WaveRecorder : IDisposable
{
    private BinaryWriter writer;
    private int byteCount;
    private IAudioStream audioStream;
    bool writeHeadersToStream;

    /// <summary>
    /// Starts recording WAVE format audio from the audio stream.
    /// </summary>
    /// <param name="stream">A <see cref="IAudioStream"/> that provides the audio data.</param>
    /// <param name="recordStream">The stream the audio will be written to.</param>
    /// <param name="filePath">The full path of the file to record audio to.</param>
    /// <param name="writeHeaders"><c>false</c> (default) Write WAV headers to stream at the end of recording.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task StartRecorder(IAudioStream stream, Stream recordStream, bool writeHeaders = false)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (recordStream == null)
        {
            throw new ArgumentNullException(nameof(recordStream));
        }

        writeHeadersToStream = writeHeaders;

        try
        {
            // if we're restarting, let's see if we have an existing stream configured that can be stopped
            if (this.audioStream != null)
            {
                await this.audioStream.Stop();
            }

            this.audioStream = stream;
            this.writer = new BinaryWriter(recordStream, Encoding.UTF8);

            this.byteCount = 0;
            this.audioStream.OnBroadcast += this.OnStreamBroadcast;
            this.audioStream.OnActiveChanged += this.StreamActiveChanged;

            if (!this.audioStream.Active)
            {
                await this.audioStream.Start();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in WaveRecorder.StartRecorder(): {0}", ex.Message);

            this.StopRecorder();
            throw;
        }
    }

    /// <summary>
    /// Stops recording WAV audio from the underlying <see cref="IAudioStream"/> and finishes writing the WAV file.
    /// </summary>
    public void StopRecorder()
    {
        try
        {
            if (this.audioStream != null)
            {
                this.audioStream.OnBroadcast -= this.OnStreamBroadcast;
                this.audioStream.OnActiveChanged -= this.StreamActiveChanged;
            }

            if (this.writer != null)
            {
                if (writeHeadersToStream && this.writer.BaseStream.CanWrite && this.writer.BaseStream.CanSeek)
                {
                    // now that audio is finished recording, write a WAV/RIFF header at the beginning of the file
                    this.writer.Seek(0, SeekOrigin.Begin);
                    AudioFunctions.WriteWavHeader(this.writer, this.audioStream.ChannelCount, this.audioStream.SampleRate, this.audioStream.BitsPerSample, this.byteCount);
                }

                this.writer.Dispose(); // this should properly close/dispose the underlying stream as well
                this.writer = null;
            }

            this.audioStream = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error during StopRecorder: {0}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.StopRecorder();
    }

    private void StreamActiveChanged(object sender, bool active)
    {
        if (!active)
        {
            this.StopRecorder();
        }
    }

    private void OnStreamBroadcast(object sender, byte[] bytes)
    {
        try
        {
            if (this.writer != null)
            {
                this.writer.Write(bytes);
                this.byteCount += bytes.Length;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in WaveRecorder.OnStreamBroadcast(): {0}", ex.Message);

            this.StopRecorder();
        }
    }
}