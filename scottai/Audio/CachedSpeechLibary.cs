using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace VoiceChat;

internal class CachedSpeechLibrary
{
    public required AudioData Hmm { get; init; }
    public required AudioData Um { get; init; }
    public required AudioData Goodbye { get; init; }
    public required AudioData GetBackToYou { get; init; }
    public required AudioData SureDropItInChat { get; init; }
    public required AudioData Greeting { get; init; }

    public AudioData RandomFiller()
        => Random.Shared.Next(0, 2) == 0 ? Um : Hmm;
    public static async Task<CachedSpeechLibrary> BuildAsync(string cacheFolder, SpeechSynthesizer speechSynthesizer, ILogger logger)
    {
        if (!Directory.Exists(cacheFolder)) Directory.CreateDirectory(cacheFolder);

        async Task<AudioData> quickSynth(string text)
        {
            var filename = Path.Combine(cacheFolder, HashFileName(text));
            if (File.Exists(filename) && AudioData.TryLoadFromFile(filename, out var data) && data != null)
            {
                logger.LogInformation($"Loaded \"{text}\" from cache.");
                return data;
            }

            using var result = await speechSynthesizer.SpeakTextAsync(text);
            var newData = new AudioData(result);
            newData.SaveToFile(filename);
            logger.LogInformation($"Built new \"{text}\".");
            return newData;
        }
        return new CachedSpeechLibrary
        {
            Hmm = await quickSynth("hmm"),
            Um = await quickSynth("um"),
            GetBackToYou = await quickSynth("I'll have to get back to you on that."),
            Goodbye = await quickSynth("Have a great day."),
            Greeting = await quickSynth("Hi there, how can I help?"),
            SureDropItInChat = await quickSynth("Sure, let me put that in the chat.")
        };
    }

    private static string HashFileName(string text)
    {
        byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(text);
        byte[] hashBytes = MD5.HashData(inputBytes);
        return Convert.ToHexString(hashBytes) + ".cache";
    }

    public class AudioData
    {
        private static readonly byte[] _header = new byte[] { 0x80, 0x90, 0x11 };
        public byte[] Data { get; }
        public int AudioDuration { get; }
        public AudioData(SpeechSynthesisResult result)
        {
            this.Data = result.AudioData;
            this.AudioDuration = (int)result.AudioDuration.TotalMilliseconds;
        }
        private AudioData(byte[] data, int length)
        {
            this.Data = data;
            this.AudioDuration = length;
        }

        public void SaveToFile(string filename)
        {
            using var stream = File.OpenWrite(filename);
            stream.Write(_header);
            stream.Write(BitConverter.GetBytes(AudioDuration));
            stream.Write(Data);
        }
        public static bool TryLoadFromFile(string filename, out AudioData? data)
        {
            data = null;
            try
            {
                if (File.Exists(filename)) return false;
                var rawData = File.ReadAllBytes(filename);
                for (int i = 0; i < _header.Length; i++)
                {
                    if (rawData[i] != _header[i]) return false;
                }
                int length = BitConverter.ToInt32(rawData, 3);
                data = new AudioData(rawData[11..], length);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
