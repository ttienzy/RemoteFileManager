namespace RemoteFileManager.Core.Exceptions;

public class InsufficientStorageException : DomainException
{
    public InsufficientStorageException(string message = "Insufficient storage space")
        : base(message, "INSUFFICIENT_STORAGE")
    {
    }
}