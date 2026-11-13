namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class MonitoringOptions
{
    public bool Enabled { get; set; }
    public string PythonPath { get; set; } = "";
    public string WorkerPath { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public string FfprobePath { get; set; } = "";
    public string FontPath { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public int MaxLiveCameras { get; set; } = 4;
    public int WorkerTimeoutSeconds { get; set; } = 60;
    public int LiveInferenceThreads { get; set; } = 4;
    public string InferenceProviders { get; set; } = "DmlExecutionProvider,CPUExecutionProvider";
    public int MaxVideoMegabytes { get; set; } = 200;
    public int MaxVideoSeconds { get; set; } = 600;
    public int MaxActiveJobsPerUser { get; set; } = 2;
    public int MaxQueuedJobs { get; set; } = 10;
    public bool Configured => Enabled && File.Exists(PythonPath) && File.Exists(WorkerPath) && File.Exists(ModelPath);
}
