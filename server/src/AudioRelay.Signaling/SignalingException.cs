namespace AudioRelay.Signaling;

/// <summary>Thrown by a signaling handler to surface a specific HTTP status to the client.</summary>
public sealed class SignalingException : Exception
{
    public int StatusCode { get; }

    public SignalingException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}
