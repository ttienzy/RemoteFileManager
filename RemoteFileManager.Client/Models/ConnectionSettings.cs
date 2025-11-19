namespace RemoteFileManager.Client.Models;

public class ConnectionSettings
{
    public string ServerAddress { get; set; } = "http://localhost:5000";
    public string Username { get; set; } = string.Empty;
    public bool RememberConnection { get; set; }
}