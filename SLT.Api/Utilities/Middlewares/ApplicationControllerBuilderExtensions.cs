namespace SLT.Api.Utilities.Middlewares
{ 
    public static class ApplicationControllerBuilderExtensions
    {
        public static void UseSecurityStamp(this IApplicationBuilder builder)
        {
            //builder.UseMiddleware<SecurityStampMiddleware>();
        }
        public static void UseProductionCors(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<ProductionCorsMiddleware>();
        }

        public static void UseJWTBlackList(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<JwtBlacklistMiddleware>();
        }

        public static void UseRequestLogger(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
