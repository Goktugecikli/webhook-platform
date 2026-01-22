using System;

public sealed class WebhookDispatchHttpException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public WebhookDispatchHttpException(int statusCode, string? responseBody)
        : base($"Remote returned {statusCode}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}