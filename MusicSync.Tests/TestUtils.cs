using System.Reflection;

namespace MusicSync.Tests;

public class TestUtils
{
    private const string
        ResourceName = "MusicSync.Tests.Fixtures.silent_audio.mp3"; // Adjust to your actual namespace and path

    public static byte[] getMp3Bytes()
    {
        // Get the current assembly where the resource is embedded
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            throw new NullReferenceException($"{ResourceName} not found");
        }

        // Now you can read from the stream (e.g., convert to byte array)
        using var memoryStream = new MemoryStream();

        stream.CopyTo(memoryStream);
        var mp3Data = memoryStream.ToArray();

        return mp3Data;
    }
}
