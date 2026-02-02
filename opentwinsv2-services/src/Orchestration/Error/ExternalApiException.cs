using System.Net;

namespace OpenTwinsV2.Orchestration.Error
{
    public class ExternalApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ErrorContent { get; }

        public ExternalApiException(HttpStatusCode statusCode, string errorContent) 
            : base($"External API failed with status {statusCode}")
        {
            StatusCode = statusCode;
            ErrorContent = errorContent;
        }
    }
}