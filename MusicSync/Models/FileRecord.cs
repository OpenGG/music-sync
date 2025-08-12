using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicSync.Models;

[Table("FileRecords")]
public class FileRecord
{
    [Key]
    public int Id { get; set; }
    [Required]
    public string AbsolutePath { get; set; }
    [Required]
    public long MTime { get; set; }
    public string? ContentHash { get; set; }
    public string? AudioFingerprint { get; set; }
    [Required]
    public ProcessingStatus Status { get; set; }
    [Required]
    public DateTime LastProcessedAt { get; set; }
}
