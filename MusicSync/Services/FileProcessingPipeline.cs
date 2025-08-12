using System.Threading.Tasks.Dataflow;
using MusicSync.Models;
using MusicSync.Utils;

namespace MusicSync.Services;

public class FileProcessingPipeline(
    Config config,
    DrmPluginLoader pluginLoader,
    TemporaryDirectory rootTempDir,
    string sourceDir)
{
    // Limiting I/O concurrency is crucial to avoid overwhelming the disk.
    private const int MaxIoConcurrency = 4;

    // Allow more concurrency for CPU-bound tasks.
    private static readonly int MaxCpuConcurrency = Environment.ProcessorCount;

    public (ITargetBlock<string> Head, Task Completion) CreatePipeline(DatabaseService db, HashService hashService)
    {
        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            BoundedCapacity = 1000
        };

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

        // Block 1: Initial check - File path to FileContext
        var metadataCheckBlock = new TransformBlock<string, FileContext?>(async path =>
        {
            var mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
            if (await db.CheckMetadataExistsAsync(path, mtime))
            {
                // Already processed and up-to-date, skip everything.
                return null;
            }

            return new FileContext
            {
                FilePath = path,
                MTime = mtime,
                RelativePath = Path.GetRelativePath(sourceDir, path),
                Status = ProcessingStatus.Pending
            };
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = MaxIoConcurrency });

        // Block 2: DRM handling and file type validation
        var drmBlock = new TransformBlock<FileContext, FileContext>(context =>
        {
            var plugin = pluginLoader.Resolve(context.FilePath);
            string? processingPath;

            if (plugin != null)
            {
                context.DrmPlugin = plugin;
                var tempDir = rootTempDir.CreateTemporaryDirectory();
                processingPath = plugin.Decrypt(context.FilePath, tempDir, config.MusicExtensions.ToArray());

                if (processingPath == null)
                {
                    context.Status = ProcessingStatus.DrmFailure;
                    return context; // Failed, pass to sink
                }

                context.Status = ProcessingStatus.DrmSuccess;
                context.DecryptedFilePath = processingPath;
            }
            else if (config.MusicExtensions.Contains(Path.GetExtension(context.FilePath).ToLower()))
            {
                // This is a standard, non-DRM music file
                context.DecryptedFilePath = context.FilePath;
            }
            else
            {
                context.Status = ProcessingStatus.Unsupported;
            }

            return context;
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = MaxCpuConcurrency });

        // Block 3: Content & Fingerprint Hashing
        var hashBlock = new TransformBlock<FileContext, FileContext>(async context =>
        {
            if (context.Status is ProcessingStatus.DrmFailure or ProcessingStatus.Unsupported)
            {
                return context;
            }

            if (context.DecryptedFilePath == null)
            {
                context.Status = ProcessingStatus.HashFailure; // Should not happen
                return context;
            }

            context.Status = ProcessingStatus.ContentCheck;
            var contentHashTask = hashService.ComputeContentHashAsync(context.DecryptedFilePath);
            var fingerprintTask = hashService.ComputeAudioFingerprintAsync(context.DecryptedFilePath);

            try
            {
                await Task.WhenAll(contentHashTask, fingerprintTask);

                context.ContentHash = await contentHashTask;
                context.AudioFingerprint = await fingerprintTask;

                if (string.IsNullOrEmpty(context.ContentHash) || string.IsNullOrEmpty(context.AudioFingerprint))
                {
                    context.Status = ProcessingStatus.HashFailure;
                }
            }
            catch
            {
                context.Status = ProcessingStatus.HashFailure;
            }

            return context;
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = MaxCpuConcurrency });

        // Block 4: Check if hashes already exist in DB
        var hashCheckBlock = new TransformBlock<FileContext, FileContext>(async context =>
        {
            if (context.Status is not (ProcessingStatus.ContentCheck or ProcessingStatus.DrmSuccess or ProcessingStatus.Pending))
            {
                return context; // Pass through failures
            }

            var (contentHashExists, audioFingerprintExists) = await db.CheckHashesAsync(context.ContentHash!, context.AudioFingerprint!);

            if (contentHashExists) context.Status = ProcessingStatus.SkippedContent;
            else if (audioFingerprintExists) context.Status = ProcessingStatus.SkippedFingerprint;

            return context;
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = MaxIoConcurrency });

        // Block 5: Copy file to destination
        var copyBlock = new TransformBlock<FileContext, FileContext>(context =>
        {
            if (context.Status is not (ProcessingStatus.ContentCheck or ProcessingStatus.DrmSuccess or ProcessingStatus.Pending))
            {
                return context; // Pass through things that should be skipped or failed
            }

            var name = Path.GetFileNameWithoutExtension(context.FilePath);
            var targetDir = Path.Join(config.MusicDestDir, Path.GetDirectoryName(context.RelativePath) ?? string.Empty);
            Directory.CreateDirectory(targetDir);

            var destPath = Path.Join(targetDir, name + Path.GetExtension(context.DecryptedFilePath!));
            var finalPath = PreventOverwrite(destPath);

            if (context.DrmPlugin != null) // It was a decrypted file
            {
                File.Move(context.DecryptedFilePath!, finalPath, true);
            }
            else
            {
                File.Copy(context.DecryptedFilePath!, finalPath, true);
            }

            context.Status = ProcessingStatus.Processed;
            return context;
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = MaxIoConcurrency });

        // Final Block: Update database with results
        var finalBlock = new ActionBlock<FileContext>(async context =>
        {
            await db.BatchUpsertRecordsAsync([context]);
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = MaxIoConcurrency });

        // Linking the pipeline
        metadataCheckBlock.LinkTo(DataflowBlock.NullTarget<FileContext?>(), context => context == null);
        metadataCheckBlock.LinkTo(drmBlock, linkOptions, context => context != null);

        drmBlock.LinkTo(hashBlock, linkOptions);
        hashBlock.LinkTo(hashCheckBlock, linkOptions);
        hashCheckBlock.LinkTo(copyBlock, linkOptions);
        copyBlock.LinkTo(finalBlock, linkOptions);

        return (metadataCheckBlock, finalBlock.Completion);
    }

    private static string PreventOverwrite(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        for (var i = 1; ; ++i)
        {
            var uniqName =
                $"{Path.GetFileNameWithoutExtension(path)} ({i}){Path.GetExtension(path)}";

            var uniqPath =
                Path.Join(Path.GetDirectoryName(path), uniqName);

            if (!File.Exists(uniqPath))
            {
                return uniqPath;
            }
        }
    }
}
