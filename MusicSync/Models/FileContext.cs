using MusicSync.Plugins;

namespace MusicSync.Models;

public class FileContext
{
    public string FilePath { get; init; }
    public long MTime { get; init; }
    public string RelativePath { get; set; }
    public string? ContentHash { get; set; } // BLAKE3
    public string? AudioFingerprint { get; set; } // SHA256
    public ProcessingStatus Status { get; set; }
    public DrmPlugin? DrmPlugin { get; set; }
    public string? DecryptedFilePath { get; set; } // DRM decrypted temporary file path
}
