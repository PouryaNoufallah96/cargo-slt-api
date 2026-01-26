using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Utilities.Swagger
{
    class ApplySecurityRequirementsToActionsFilter : IOperationFilter
    {
        #region Constructor
        private readonly string _schemeName;
        private readonly List<Type> _attributesThatShouldHaveSecurityRequirement;
        private readonly OpenApiSecurityScheme _scheme;
        public ApplySecurityRequirementsToActionsFilter(OpenApiSecurityScheme scheme, List<Type> types, string schemeName = "Bearer")
        {
            _scheme = scheme;
            _schemeName = schemeName;
            _attributesThatShouldHaveSecurityRequirement = types;
        }
        #endregion 

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var filters = context.ApiDescription.ActionDescriptor.FilterDescriptors;

            foreach (var attribute in _attributesThatShouldHaveSecurityRequirement)
            {
                if (!filters.Any(q => q.Filter.GetType().FullName == attribute.FullName))
                {
                    return;
                }
            }

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement { { _scheme, Array.Empty<string>() } }
            };
        }
    }
}