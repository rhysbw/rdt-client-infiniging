using RdtClient.Service.Exceptions;
using Xunit;

namespace RdtClient.Service.Test.Exceptions;

public class RealDebridFailFastExceptionTest
{
    [Fact]
    public void Constructor_WithStatusAndMessage_ShouldSetProperties()
    {
        // Arrange
        var status = "infringing";
        var message = "Real-Debrid rejected this torrent";

        // Act
        var exception = new RealDebridFailFastException(status, message);

        // Assert
        Assert.Equal(status, exception.Status);
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Constructor_WithStatusMessageAndInnerException_ShouldSetProperties()
    {
        // Arrange
        var status = "virus";
        var message = "Real-Debrid detected virus";
        var innerException = new Exception("Inner error");

        // Act
        var exception = new RealDebridFailFastException(status, message, innerException);

        // Assert
        Assert.Equal(status, exception.Status);
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
    }
}