using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

namespace Utilities.Swagger
{
    public class AddAdditionalResponseExampleFilter : IOperationFilter
    {
        private readonly responses[] _responses;

        public AddAdditionalResponseExampleFilter(responses[] responses)
        {
            _responses = responses;
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            foreach (var response in _responses)
            {
                if (response == responses.badRequest)
                    operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Bad Request" });
                else if (response == responses.forbidden)
                    operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
                else if (response == responses.unauthorized)
                    operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
                else if (response == responses.internalServerError)
                    operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Internal Server Error" });
                else if (response == responses.badGateway)
                    operation.Responses.TryAdd("502", new OpenApiResponse { Description = "Bad Gateway" });
                else
                    operation.Responses.TryAdd("504", new OpenApiResponse { Description = "Gateway Timeout" });
            }
        }
    }
    public enum responses { badRequest, unauthorized, forbidden, internalServerError, badGateway, gatewayTimeout }
}
