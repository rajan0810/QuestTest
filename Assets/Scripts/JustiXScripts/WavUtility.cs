using UnityEngine;
using System;
using System.IO;

public static class WavUtility
{
    // Converts an AudioClip to a WAV byte array (for sending to Backend)
    public static byte[] FromAudioClip(float[] samples, int frequency, int channels)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(memoryStream))
            {
                int sampleCount = samples.Length;
                int bytesPerSample = 2; // 16-bit
                int byteRate = frequency * channels * bytesPerSample;
                int blockAlign = channels * bytesPerSample;

                // RIFF header
                writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(36 + sampleCount * bytesPerSample);
                writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

                // fmt chunk
                writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16); // Sub-chunk size (16 for PCM)
                writer.Write((ushort)1); // AudioFormat (1 for PCM)
                writer.Write((ushort)channels);
                writer.Write(frequency);
                writer.Write(byteRate);
                writer.Write((ushort)blockAlign);
                writer.Write((ushort)16); // Bits per sample

                // data chunk
                writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
                writer.Write(sampleCount * bytesPerSample);

                // Write Data
                RescaleAndWrite(writer, samples);

                return memoryStream.ToArray();
            }
        }
    }

    private static void RescaleAndWrite(BinaryWriter writer, float[] samples)
    {
        // Convert float samples (-1.0 to 1.0) to 16-bit PCM
        foreach (float sample in samples)
        {
            short val = (short)(sample * 32767f);
            writer.Write(val);
        }
    }

    // Converts WAV bytes (from Backend) to an AudioClip
    // NOTE: This is a basic parser. For robust MP3/WAV loading, use a library like NAudio.
    // This is useful if your backend sends WAV.
    public static AudioClip ToAudioClip(byte[] wavFile)
    {
        // Simple WAV header parsing
        int frequency = BitConverter.ToInt32(wavFile, 24);
        int channels = BitConverter.ToInt16(wavFile, 22);
        int pos = 12;

        // Find "data" chunk
        while (!(wavFile[pos] == 100 && wavFile[pos + 1] == 97 && wavFile[pos + 2] == 116 && wavFile[pos + 3] == 97))
        {
            pos += 4;
            int chunkSize = wavFile[pos] + wavFile[pos + 1] * 256 + wavFile[pos + 2] * 65536 + wavFile[pos + 3] * 16777216;
            pos += 4 + chunkSize;
            if (pos >= wavFile.Length - 8) return null; // Error
        }
        pos += 8;

        int sampleCount = (wavFile.Length - pos) / 2; // 16 bit
        float[] samples = new float[sampleCount];

        int i = 0;
        while (pos < wavFile.Length - 1)
        {
            short val = BitConverter.ToInt16(wavFile, pos);
            samples[i] = val / 32767f;
            pos += 2;
            i++;
        }

        AudioClip clip = AudioClip.Create("GeneratedAudio", sampleCount, channels, frequency, false);
        clip.SetData(samples, 0);
        return clip;
    }
}