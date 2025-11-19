namespace RemoteFileManager.Application.DTOs;

public class OperationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }

    public static OperationResultDto Successful(string message = "Operation completed successfully")
        => new() { Success = true, Message = message };

    public static OperationResultDto Failed(string message, string? errorCode = null)
        => new() { Success = false, Message = message, ErrorCode = errorCode };
}