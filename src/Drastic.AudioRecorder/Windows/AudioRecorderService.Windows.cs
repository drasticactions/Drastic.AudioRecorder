using Windows.Storage;

namespace Drastic.AudioRecorder;

public partial class AudioRecorderService
{
    partial void Init () { }

    async Task<string> GetDefaultFilePath ()
    {
        var temporaryFolder = ApplicationData.Current.TemporaryFolder;
        var tempFile = await temporaryFolder.CreateFileAsync (DefaultFileName, CreationCollisionOption.ReplaceExisting);

        return tempFile.Path;
    }

    void OnRecordingStarting ()
    {
    }

    void OnRecordingStopped ()
    {
    }

    private IAudioStream GenerateAudioStream(int sampleRate)
    {
        return new AudioStream(sampleRate);
    }
}