using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace EdjCase.JsonRpc.Router.Swagger
{
    public class DefaultSwaggerProvider : ISwaggerProvider
    {
        private readonly ISwaggerProvider Default;

        public DefaultSwaggerProvider(ISwaggerProvider @default)
        {
            Default = @default;
        }

        public OpenApiDocument GetSwagger(string documentName, string host = null, string basePath = null) => Default.GetSwagger(documentName, host, basePath);
    }
}