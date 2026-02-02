namespace OpenTwinsV2.Orchestration.Error
{
    
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ErrorHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ExternalApiException ex)
            {
                // Capture the specific status code (e.g. 400)
                context.Response.StatusCode = (int)ex.StatusCode;
                context.Response.ContentType = "application/json";

                // Return the message from the other API directly to your client
                // You can wrap this in a customized object if you prefer
                var response = new { message = ex.ErrorContent };
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                // Fallback for generic 500 errors
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { message = $"An internal error occurred: {ex.Message}" });
            }
        }
    }
}