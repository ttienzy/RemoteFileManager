namespace RemoteFileManager.Client.Services;

public interface IDialogService
{
    void ShowMessage(string message, string title = "Information");
    void ShowError(string message, string title = "Error");
    bool ShowConfirmation(string message, string title = "Confirm");
    string? ShowInputDialog(string message, string title = "Input", string defaultValue = "");
    string? ShowOpenFileDialog(string filter = "All Files (*.*)|*.*");
    string? ShowSaveFileDialog(string filter = "All Files (*.*)|*.*", string defaultFileName = "");
}