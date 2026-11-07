namespace SmartPool.Domain.Entities;

public sealed class VideoAnalysisJob
{
    public Guid Id { get; set; }
    public Guid UploadedBy { get; set; }
    public User Uploader { get; set; } = null!;
    public string OriginalName { get; set; } = "";
    public string InputFile { get; set; } = "";
    public string? OutputFile { get; set; }
    public string Status { get; set; } = "QUEUED";
    public double Progress { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
