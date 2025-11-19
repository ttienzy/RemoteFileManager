namespace RemoteFileManager.Core.Exceptions;

public class FileNotFoundException : DomainException
{
    public FileNotFoundException(string filePath)
        : base($"File not found: {filePath}", "FILE_NOT_FOUND")
    {
    }
}