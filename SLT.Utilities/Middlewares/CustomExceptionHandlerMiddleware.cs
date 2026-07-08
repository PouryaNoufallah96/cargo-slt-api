using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Net;
using Microsoft.Extensions.Logging;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Models.Results;

namespace Utilities.Middlewares
{
    public class CustomExceptionHandlerMiddleware(RequestDelegate next, ILogger<CustomExceptionHandlerMiddleware> logger)
    {
        public async Task Invoke(HttpContext context)
        {
            string message = null;
            HttpStatusCode httpStatusCode = HttpStatusCode.InternalServerError;
            ApiResultStatusCode apiStatusCode = ApiResultStatusCode.ServerError;

            try
            {
                await next(context);
            }
            catch (BaseException exception)
            {
                httpStatusCode = exception.HttpStatusCode;
                apiStatusCode = exception.ApiStatusCode;
                message = GetClientMessage(exception);

                if ((int)httpStatusCode >= StatusCodes.Status500InternalServerError)
                {
                    logger.LogError(exception, exception.Message);
                    SentrySdk.CaptureException(exception);
                }
                else
                {
                    logger.LogWarning(
                        "Handled client error {HttpStatusCode}/{ApiStatusCode}: {Message}",
                        (int)httpStatusCode,
                        apiStatusCode,
                        message);
                }

                await WriteToResponseAsync();

            }
            catch (SecurityTokenExpiredException exception)
            {
                logger.LogWarning("Handled unauthorized error: {Message}", exception.Message);
                SetUnAuthorizeResponse();
                await WriteToResponseAsync();
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning("Handled unauthorized error: {Message}", exception.Message);
                SetUnAuthorizeResponse();
                await WriteToResponseAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, exception.Message);
                SentrySdk.CaptureException(exception);
                await WriteToResponseAsync();
            }

            async Task WriteToResponseAsync()
            {
                if (context.Response.HasStarted)
                    throw new InvalidOperationException("The response has already started, the http status code middleware will not be executed.");

                var result = new ApiResult(false, apiStatusCode, message);
                var json = JsonConvert.SerializeObject(result);

                context.Response.StatusCode = (int)httpStatusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(json);
            }

            void SetUnAuthorizeResponse()
            {
                httpStatusCode = HttpStatusCode.Unauthorized;
                apiStatusCode = ApiResultStatusCode.UnAuthorized;
            }
        }

        private static string GetClientMessage(BaseException exception)
        {
            var defaultExceptionMessage = $"Exception of type '{exception.GetType().FullName}' was thrown.";

            return string.Equals(exception.Message, defaultExceptionMessage, StringComparison.Ordinal)
                ? null
                : exception.Message;
        }
    }
}
