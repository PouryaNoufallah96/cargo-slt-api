using _CodeAssistant.Exceptions;

namespace SLT.Api.Utilities.Middlewares
{
    public class ProductionCorsMiddleware(RequestDelegate next, ILogger<ProductionCorsMiddleware> _logger)
    {

        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (!await AddCorsHeadersAsync(httpContext))
            {
                return;
            }

            await next(httpContext);
        }

        private async Task<bool> AddCorsHeadersAsync(HttpContext httpContext)
        {
            var origin = httpContext.Request.Headers["Origin"].ToString();

            var allowedOrigins = new[]
            {
            "https://s.com",
            "https://api.s.com",
            "https://app.s.com",
            "https://mp.s.com",
            "http://localhost:5132",
            "http://localhost:3000",
            "http://192.168.100.5:3000",
            "null",
            ""
            };

            if (!allowedOrigins.Contains(origin))
            {
                var requestPath = httpContext.Request.Path;
                var clientIP = httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
                _logger.LogWarning("Blocked CORS request - Origin: {Origin}, Path: {Path}, IP: {IP}, Method: {Method}",
                origin, requestPath, clientIP, httpContext.Request.Method);

                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await httpContext.Response.WriteAsync("Access Denied");
                return false;
            }

            httpContext.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            httpContext.Response.Headers.Append("Access-Control-Allow-Headers",
                "x-signalr-user-agent, Origin, X-Requested-With, Content-Type, Accept, Authorization, " +
                "ApplicationId, Nonce, Signature, " +
                "Sec-WebSocket-Version, Sec-WebSocket-Extensions, Sec-WebSocket-Key, Connection, Upgrade");
            httpContext.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

            if (httpContext.Request.Method == HttpMethods.Options)
            {
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                await httpContext.Response.CompleteAsync();
                return false;
            }

            httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            httpContext.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            httpContext.Response.Headers.Append("Content-Security-Policy",
                "frame-ancestors 'self' https://s.com https://panel.s.com https://api.s.com https://app.s.com");

            httpContext.Response.Headers.Remove("server");

            return true;
        }

        //public async Task InvokeAsync(HttpContext httpContext)
        //{
        //    await AddCorsHeaders(httpContext);

        //    if (httpContext.Request.Method == "OPTIONS")
        //    {
        //        return;
        //    }
        //    else
        //    {
        //        await next(httpContext);
        //    }
        //}


        //private async Task AddCorsHeaders(HttpContext httpContext)
        //{
        //    var origin = httpContext.Request.Headers["Origin"].ToString();

        //    var allowedOrigins = new[]
        //    {
        //        "https://rzprime.com",
        //        "https://api.rzprime.com",
        //        "https://app.rzprime.com",
        //        "https://mp.rzprime.com",
        //        "http://localhost:5132",
        //        "http://localhost:3000",
        //        "null"
        //    };

        //    if (allowedOrigins.Contains(origin))
        //    {
        //        httpContext.Response.Headers.Append("Access-Control-Allow-Origin", origin);
        //        httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        //    }
        //    else
        //    {
        //        httpContext.Response.StatusCode = 403;
        //        await httpContext.Response.WriteAsync("Access Denied");
        //        return;
        //    }



        //    httpContext.Response.Headers.Append("Access-Control-Allow-Headers",
        //        "x-signalr-user-agent, Origin, X-Requested-With, Content-Type, Accept, Authorization, " +
        //        "ApplicationId, Nonce, Signature, " +
        //        "Sec-WebSocket-Version, Sec-WebSocket-Extensions, Sec-WebSocket-Key, Connection, Upgrade");

        //    httpContext.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");


        //    if (httpContext.Request.Method == HttpMethods.Options)
        //    {
        //        httpContext.Response.StatusCode = 200;
        //        await httpContext.Response.CompleteAsync();
        //        return;
        //    }

        //    //Security headers
        //    httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        //    //httpContext.Response.Headers.Append("X-Frame-Options", "DENY");
        //    httpContext.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        //    httpContext.Response.Headers.Append("Content-Security-Policy",
        //        "frame-ancestors 'self' https://rzprime.com https://panel.rzprime.com https://api.rzprime.com https://app.rzprime.com");
        //    httpContext.Response.Headers.Remove("server");
        //}
        //private void AddCorsHeaders(HttpContext httpContext)
        //{
        //    //httpContext.Response.Headers.Append("Access-Control-Allow-Origin", new[] { "*" });
        //    //httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", new[] { "true" });

        //    var origin = httpContext.Request.Headers["Origin"].ToString();

        //    //Check if the Origin matches allowed domains
        //    //if (origin == "https://rzprime.com" || origin == "https://mp.rzprime.com" || origin == "https://api.rzprime.com")
        //    if (origin == "https://app.rzprime.com" || origin == "https://rzprime.com" || origin == "https://mp.rzprime.com" || origin == "https://api.rzprime.com"  
        //    || origin == "null" || origin == "http://localhost:5132" || origin == "http://localhost:3000")
        //    {

        //        httpContext.Response.Headers.Append("Access-Control-Allow-Origin", origin);
        //        httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        //    }
        //    else
        //    {
        //        httpContext.Response.StatusCode = 403; // Forbidden
        //        httpContext.Response.WriteAsync("Access Denied");
        //        throw new AuthorizationException("Access Denied");
        //    }

        //    // Handle OPTIONS preflight
        //    if (httpContext.Request.Method == HttpMethods.Options)
        //    {
        //        httpContext.Response.StatusCode = 200;
        //        return;
        //    }


        //    //httpContext.Response.Headers.Append("Access-Control-Allow-Origin", new[] { "*" });
        //    httpContext.Response.Headers.Append("Access-Control-Allow-Headers",
        //        "x-signalr-user-agent, Origin, X-Requested-With, Content-Type, Accept, Authorization, ApplicationId, Nonce, Signature");
        //    httpContext.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        //    //httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", "true");

        //    // Optional: Add security headers
        //    //httpContext.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self'; frame-src 'self'");
        //    httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        //    httpContext.Response.Headers.Append("X-Frame-Options", "DENY");
        //    httpContext.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        //    httpContext.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'self' https://rzprime.com https://panel.rzprime.com https://api.rzprime.com");
        //    httpContext.Response.Headers.Remove("server");
        //}


    }
}