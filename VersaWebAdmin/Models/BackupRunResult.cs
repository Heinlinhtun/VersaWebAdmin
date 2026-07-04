namespace VersaWebAdmin.Models;

public class BackupRunResult
{
    public bool IsSuccess { get; set; }
    public bool IsSkipped { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public int TotalTenants { get; set; }
    public int SuccessfulTenants { get; set; }
    public int FailedTenants { get; set; }
    public int DropboxUploadSucceededCount { get; set; }
    public int DropboxUploadFailedCount { get; set; }
    public List<BackupTenantFailure> FailedDatabases { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

public class BackupTenantFailure
{
    public string DatabaseName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
