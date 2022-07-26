using EdjCase.JsonRpc.Router.Swagger;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WebApplication3
{
    /// <summary>
    /// Расширенный провайдер для документации Swagger под JSON-RPC / JSON API схемы
    /// </summary>
    public class SwaggerProviderExtended : ISwaggerProvider
    {
        private readonly SwaggerGenerator SwaggerGenerator;

        // Something is wrong with nuget package
        // <!--<PackageReference Include = "Swashbuckle.AspNetCore" Version="6.2.3" />-->
        // it throws error also
        private readonly JsonRpcSwaggerProvider JsonRpcSwaggerProvider;

        public SwaggerProviderExtended(JsonRpcSwaggerProvider jsonRpcSwaggerProvider, SwaggerGenerator swaggerGenerator)
        {
            JsonRpcSwaggerProvider = jsonRpcSwaggerProvider;
            SwaggerGenerator = swaggerGenerator;
        }

        public OpenApiDocument GetSwagger(string documentName, string host = null, string basePath = null) => documentName != "jsonrpc"
                ? SwaggerGenerator.GetSwagger(documentName, host, basePath)
                : JsonRpcSwaggerProvider.GetSwagger(documentName, host, basePath);
    }

}
