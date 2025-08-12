namespace MusicSync.Models;

public enum ProcessingStatus
{
    Pending,
    MetadataCheck,
    ContentCheck,
    FingerprintCheck,
    Processing,
    Processed,
    SkippedMetadata,
    SkippedContent,
    SkippedFingerprint,
    DrmSuccess,
    DrmFailure,
    HashFailure,
    Unsupported
}
