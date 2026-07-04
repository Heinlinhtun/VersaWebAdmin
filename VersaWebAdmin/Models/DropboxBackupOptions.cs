namespace VersaWebAdmin.Models;

public class DropboxBackupOptions
{
    public const string SectionName = "DropboxBackup";

    public bool Enabled { get; set; } = true;
    public string AccessToken { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string RootPath { get; set; } = "/VersaBackups";
    public string MySqlDumpPath { get; set; } = "mysqldump";
    public int ProcessTimeoutSeconds { get; set; } = 1800;
    public int DropboxUploadTimeoutSeconds { get; set; } = 300;
}
