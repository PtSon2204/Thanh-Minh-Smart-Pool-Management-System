namespace SmartPool.Infrastructure.SafetyMonitoring;

public sealed class MonitoringOptions
{
    public bool Enabled { get; set; }
    public string PythonPath { get; set; } = "";
    public string WorkerPath { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public string FontPath { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public int MaxLiveCameras { get; set; } = 4;
    public int LiveReconnectGraceSeconds { get; set; } = 15;
    public int MaxPendingAlertSnapshots { get; set; } = 256;
    public int WorkerTimeoutSeconds { get; set; } = 60;
    public int LiveInferenceThreads { get; set; } = 4;
    public string InferenceProviders { get; set; } = "DmlExecutionProvider,CPUExecutionProvider";
    public bool Configured => Enabled && File.Exists(PythonPath) && File.Exists(WorkerPath) && File.Exists(ModelPath);
}
