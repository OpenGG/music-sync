using MusicSync.Utils;

namespace MusicSync.Services;

public class HashService
{
    public virtual async Task<string> ComputeContentHashAsync(string filePath)
    {
        return await HashUtil.ComputeBlake3HashAsync(filePath);
    }

    public virtual async Task<string?> ComputeAudioFingerprintAsync(string filePath)
    {
        return await FfmpegUtil.GetAudioHashAsync(filePath);
    }
}
