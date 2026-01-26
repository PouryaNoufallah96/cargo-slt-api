using Microsoft.AspNetCore.Http;
using System.Text;
using Utilities.Services.Contracts;
using Utilities.Models.Settings;
using Utilities.Utilities;
using Utilities.Exceptions.Common;


namespace Utilities.Middlewares
{
    public class SignatureMiddleware(RequestDelegate _next, ISignatureService _signatureService,
        INonceService _nonceService, ApplicationPoolSettings _applicationPool)
    {
        public async Task InvokeAsync(HttpContext context)
        {

            if (context.Request.Path.StartsWithSegments("/hubs/prices") || 
                context.Request.Path.StartsWithSegments("/hubs/predictions") ||
                context.Request.Path.StartsWithSegments("/api/v1/File/DownloadFile")) 
            {
                await _next(context);
                return;
            }

            var headers = context.Request.Headers;

            var applicationId = headers["ApplicationId"].FirstOrDefault();
            var nonce = headers["Nonce"].FirstOrDefault();
            var signature = headers["Signature"].FirstOrDefault();

            var application = _applicationPool.Applications
                .FirstOrDefault(q => q.ApplicationId == applicationId);


            if (application == null)
                throw new BadRequestException("Invalid Application");

            if (string.IsNullOrEmpty(signature))
                throw new BadRequestException("Signature is not specified");

            bool isMaster = signature == application.MasterSignature;

            if (!isMaster && string.IsNullOrEmpty(nonce))
                throw new BadRequestException("Nonce is not specified");


            if (!isMaster)
            {
                if (!_nonceService.TryUse(nonce, TimeSpan.FromMinutes(5)))
                    throw new BadRequestException("Duplicate nonce");

                byte[] signatureBytes = Convert.FromBase64String(signature);

                bool isValidSignature = _signatureService.Verify(
                    Encoding.UTF8.GetBytes(application.PreSharedKey),
                    Encoding.UTF8.GetBytes(nonce ?? string.Empty),
                    signatureBytes
                );

                if (!isValidSignature)
                    throw new BadRequestException("Signature is not valid");
            }

            await _next(context);
        }
    }
}