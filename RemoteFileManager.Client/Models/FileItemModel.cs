using CommunityToolkit.Mvvm.ComponentModel;
using Google.Protobuf;
using System.IO;

namespace RemoteFileManager.Client.Models;

public partial class FileItemModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private DateTime _createdDate;

    [ObservableProperty]
    private DateTime _modifiedDate;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private string _extension = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public string SizeFormatted => FormatFileSize(Size);

    public string Icon => IsDirectory ? "📁" : GetFileIcon(Extension);

    private static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "-";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    private static string GetFileIcon(string extension)
    {
        return extension.ToLower() switch
        {
            ".txt" => "📄",
            ".pdf" => "📕",
            ".doc" or ".docx" => "📘",
            ".xls" or ".xlsx" => "📗",
            ".jpg" or ".jpeg" or ".png" or ".gif" => "🖼️",
            ".mp3" or ".wav" or ".flac" => "🎵",
            ".mp4" or ".avi" or ".mkv" => "🎬",
            ".zip" or ".rar" or ".7z" => "📦",
            _ => "📄"
        };
    }
}