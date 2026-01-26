//using _CodeAssistant.Enums;
//using _CodeAssistant.Exceptions;
//using _CodeAssistant.Extensions;
//using _CodeAssistant.Filters;
//using _CodeAssistant.Utilities;
//using Herkoul.Domain.Repositories.Contracts;

//namespace Herkoul.Api.Utilities.Middlewares
//{
//    public class SecurityStampMiddleware(RequestDelegate _next, IUserRepository _userRepository)
//    {
//        public async Task InvokeAsync(HttpContext context)
//        {
//            var authorizeAttribute = context.GetEndpoint()?.Metadata.GetMetadata<AuthorizeAttribute>();

//            if (authorizeAttribute != null)
//            {
//                var jwtToken = context.GetToken();
//                var publicKey = jwtToken.GetClaim(Claims.PublicKey.ToDisplay());
//                var securityStamp = jwtToken.GetClaim(Claims.SecurityStamp.ToDisplay());

//                if (publicKey != null && securityStamp != null)
//                {
//                    var user = await _userRepository.GetUserByIdAsync(publicKey.Value);
//                    if (user != null && user.SecurityStamp != securityStamp.Value)
//                        throw new AuthorizationException("خطای عدم دسترسی");
//                }
//            }

//            await _next(context);
//        }
//    }
//}
