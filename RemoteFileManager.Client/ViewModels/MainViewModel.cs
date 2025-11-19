using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteFileManager.Client.Models;
using RemoteFileManager.Client.Services.GrpcServices;
using RemoteFileManager.Client.Services.Helpers;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace RemoteFileManager.Client.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FileManagerGrpcClient _fileManagerClient;
    private readonly StreamingGrpcClient _streamingClient;

    [ObservableProperty]
    private string _currentPath = "/";

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileItemModel> _files = new();

    [ObservableProperty]
    private FileItemModel? _selectedFile;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _uploadProgress;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public MainViewModel(
        FileManagerGrpcClient fileManagerClient,
        StreamingGrpcClient streamingClient,
        string username)
    {
        _fileManagerClient = fileManagerClient;
        _streamingClient = streamingClient;
        _username = username;

        // Load initial files
        _ = LoadFilesAsync();
    }

    [RelayCommand]
    private async Task LoadFilesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading files...";

        try
        {
            var response = await _fileManagerClient.ListFilesAsync(CurrentPath);

            Files.Clear();
            foreach (var file in response.Files)
            {
                Files.Add(new FileItemModel
                {
                    Name = file.Name,
                    FullPath = file.FullPath,
                    Size = file.Size,
                    CreatedDate = DateTime.Parse(file.CreatedDate),
                    ModifiedDate = DateTime.Parse(file.ModifiedDate),
                    IsDirectory = file.IsDirectory,
                    Extension = file.Extension
                });
            }

            StatusMessage = $"Loaded {Files.Count} items";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to load files: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenFileAsync(FileItemModel? file)
    {
        if (file == null) return;

        if (file.IsDirectory)
        {
            CurrentPath = file.FullPath;
            await LoadFilesAsync();
        }
        else
        {
            // Download and open file
            await DownloadFileAsync(file);
        }
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (CurrentPath == "/" || CurrentPath == "\\") return;

        var parentPath = Path.GetDirectoryName(CurrentPath);
        if (string.IsNullOrEmpty(parentPath))
        {
            CurrentPath = "/";
        }
        else
        {
            CurrentPath = parentPath.Replace("\\", "/");
        }

        await LoadFilesAsync();
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        //var dialog = new Microsoft.VisualBasic.Interaction();
        var folderName = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter folder name:",
            "Create New Folder",
            "New Folder");

        if (string.IsNullOrWhiteSpace(folderName)) return;

        IsLoading = true;
        StatusMessage = "Creating folder...";

        try
        {
            var result = await _fileManagerClient.CreateFolderAsync(CurrentPath, folderName);

            if (result.Success)
            {
                StatusMessage = "Folder created successfully";
                await LoadFilesAsync();
            }
            else
            {
                MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create folder: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteFileAsync()
    {
        if (SelectedFile == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedFile.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        StatusMessage = "Deleting...";

        try
        {
            var deleteResult = await _fileManagerClient.DeleteFileAsync(SelectedFile.FullPath);

            if (deleteResult.Success)
            {
                StatusMessage = "Deleted successfully";
                await LoadFilesAsync();
            }
            else
            {
                MessageBox.Show(deleteResult.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RenameFileAsync()
    {
        if (SelectedFile == null) return;

        var newName = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter new name:",
            "Rename",
            SelectedFile.Name);

        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedFile.Name) return;

        IsLoading = true;
        StatusMessage = "Renaming...";

        try
        {
            var result = await _fileManagerClient.RenameFileAsync(SelectedFile.FullPath, newName);

            if (result.Success)
            {
                StatusMessage = "Renamed successfully";
                await LoadFilesAsync();
            }
            else
            {
                MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to rename: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UploadFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select file to upload",
            Filter = "All Files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        IsUploading = true;
        UploadProgress = 0;
        StatusMessage = "Uploading...";

        try
        {
            var progress = new Progress<int>(percent =>
            {
                UploadProgress = percent;
                StatusMessage = $"Uploading... {percent}%";
            });

            var response = await _streamingClient.UploadFileAsync(
                dialog.FileName,
                CurrentPath,
                progress);

            if (response.Success)
            {
                StatusMessage = $"Upload complete: {ChunkHelper.FormatFileSize(response.FileSize)}";
                await LoadFilesAsync();

                MessageBox.Show(
                    $"File uploaded successfully!\nSize: {ChunkHelper.FormatFileSize(response.FileSize)}",
                    "Upload Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(response.Message, "Upload Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Upload failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsUploading = false;
            UploadProgress = 0;
        }
    }

    [RelayCommand]
    private async Task DownloadFileAsync(FileItemModel? file = null)
    {
        var targetFile = file ?? SelectedFile;
        if (targetFile == null || targetFile.IsDirectory) return;

        var dialog = new SaveFileDialog
        {
            Title = "Save file as",
            FileName = targetFile.Name,
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        IsDownloading = true;
        DownloadProgress = 0;
        StatusMessage = "Downloading...";

        try
        {
            var progress = new Progress<int>(percent =>
            {
                DownloadProgress = percent;
                StatusMessage = $"Downloading... {percent}%";
            });

            await _streamingClient.DownloadFileAsync(
                targetFile.FullPath,
                dialog.FileName,
                progress);

            StatusMessage = "Download complete";

            MessageBox.Show(
                $"File downloaded successfully to:\n{dialog.FileName}",
                "Download Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Ask to open file
            var openResult = MessageBox.Show(
                "Do you want to open the file?",
                "Open File",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (openResult == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dialog.FileName,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
        }
    }

    [RelayCommand]
    private async Task SearchFilesAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadFilesAsync();
            return;
        }

        IsLoading = true;
        StatusMessage = $"Searching for '{SearchText}'...";

        try
        {
            var response = await _fileManagerClient.SearchFilesAsync(SearchText, CurrentPath);

            Files.Clear();
            foreach (var file in response.Results)
            {
                Files.Add(new FileItemModel
                {
                    Name = file.Name,
                    FullPath = file.FullPath,
                    Size = file.Size,
                    CreatedDate = DateTime.Parse(file.CreatedDate),
                    ModifiedDate = DateTime.Parse(file.ModifiedDate),
                    IsDirectory = file.IsDirectory,
                    Extension = file.Extension
                });
            }

            StatusMessage = $"Found {Files.Count} items";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Search failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadFilesAsync();
    }
}