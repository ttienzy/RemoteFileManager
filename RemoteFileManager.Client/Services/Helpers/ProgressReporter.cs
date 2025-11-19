namespace RemoteFileManager.Client.Services.Helpers;

public class ProgressReporter : IProgress<int>
{
    public event EventHandler<int>? ProgressChanged;

    public void Report(int value)
    {
        ProgressChanged?.Invoke(this, value);
    }
}