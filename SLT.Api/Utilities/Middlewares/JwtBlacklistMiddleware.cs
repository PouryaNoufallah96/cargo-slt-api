using SLT.Services._User.DTOs.Storages;
using Utilities.Exceptions;

namespace SLT.Api.Utilities.Middlewares
{
    public class JwtBlacklistMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtBlacklistMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, JwtBlacklistStorage blacklist)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(token) && blacklist.IsTokenBlacklisted(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                throw new AuthorizationException("Please login again.");
            }

            await _next(context);
        }

    }
}
