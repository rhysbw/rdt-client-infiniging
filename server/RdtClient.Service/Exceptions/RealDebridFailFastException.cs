namespace RdtClient.Service.Exceptions;

/// <summary>
/// Exception thrown when Real-Debrid reports a fatal status that should cause immediate failure
/// instead of queuing the torrent for background processing.
/// </summary>
public class RealDebridFailFastException : Exception
{
    public string Status { get; }
    
    public RealDebridFailFastException(string status, string message) : base(message)
    {
        Status = status;
    }
    
    public RealDebridFailFastException(string status, string message, Exception innerException) : base(message, innerException)
    {
        Status = status;
    }
}