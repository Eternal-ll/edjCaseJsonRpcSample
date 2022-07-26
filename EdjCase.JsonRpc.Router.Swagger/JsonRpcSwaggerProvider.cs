using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Router.Abstractions;
using EdjCase.JsonRpc.Router.Swagger.Extensions;
using EdjCase.JsonRpc.Router.Swagger.Models;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EdjCase.JsonRpc.Router.Swagger
{
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.Extensions.DependencyInjection;

    public class JsonRpcSwaggerProvider : ISwaggerProvider
	{
		private readonly IApiDescriptionGroupCollectionProvider apiDescriptionsProvider;
		private readonly SwaggerGeneratorOptions options;
		private readonly IServiceProvider serviceProvider;

		private readonly ISchemaGenerator schemaGenerator;
		private readonly SwaggerConfiguration swagerOptions;
		private readonly IServiceScopeFactory scopeFactory;
		private readonly IXmlDocumentationService xmlDocumentationService;
		private OpenApiDocument? cacheDocument;
		private JsonNamingPolicy namePolicy;

        public JsonRpcSwaggerProvider(
            ISchemaGenerator schemaGenerator,
            IXmlDocumentationService xmlDocumentationService,
            IOptions<SwaggerConfiguration> swaggerOptions,
            IServiceScopeFactory scopeFactory, IApiDescriptionGroupCollectionProvider apiDescriptionsProvider, SwaggerGeneratorOptions options, IServiceProvider serviceProvider)
        {
            this.schemaGenerator = schemaGenerator;
            this.swagerOptions = swaggerOptions.Value;
            this.namePolicy = swaggerOptions.Value.NamingPolicy;
            this.scopeFactory = scopeFactory;
            this.xmlDocumentationService = xmlDocumentationService;
            this.apiDescriptionsProvider = apiDescriptionsProvider;
            this.options = options;
            this.serviceProvider = serviceProvider;
        }

        private List<UniqueMethod> GetUniqueKeyMethodPairs(RpcRouteMetaData metaData)
		{
			List<UniqueMethod> methodList = this.Convert(metaData.BaseRoute, path: null).ToList();

			foreach ((RpcPath path, IReadOnlyList<IRpcMethodInfo> pathRoutes) in metaData.PathRoutes)
			{
				methodList.AddRange(this.Convert(pathRoutes, path));
			}

			return methodList;
		}

		private IEnumerable<UniqueMethod> Convert(IEnumerable<IRpcMethodInfo> routeInfo, RpcPath? path)
		{
			//group by name for generate unique url similar method names
			foreach (IGrouping<string, IRpcMethodInfo> methodsGroup in routeInfo.GroupBy(x => x.Name))
			{
				int? methodCounter = methodsGroup.Count() > 1 ? 1 : (int?)null;
				foreach (IRpcMethodInfo methodInfo in methodsGroup)
				{
					string methodName = this.namePolicy.ConvertName(methodInfo.Name);
					string uniqueUrl = $"/{path}#{methodName}";

					if (methodCounter != null)
					{
						uniqueUrl += $"#{methodCounter++}";
					}

					yield return new UniqueMethod(uniqueUrl, methodInfo);
				}
			}
		}

		public OpenApiDocument GetSwagger(string documentName, string? host = null, string? basePath = null)
		{
            if (this.cacheDocument != null)
            {
                return this.cacheDocument;
            }

            if (!options.SwaggerDocs.TryGetValue(documentName, out OpenApiInfo info))
				throw new UnknownSwaggerDocument(documentName, options.SwaggerDocs.Select(d => d.Key));

			var schemaRepository = new SchemaRepository();
			var methodProvider = this.scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IRpcMethodProvider>();
			RpcRouteMetaData metaData = methodProvider.Get();
			var rpcMethods = metaData.PathRoutes
				.SelectMany(g => g.Value);
			var applicableApiDescriptions = apiDescriptionsProvider.ApiDescriptionGroups.Items
				.SelectMany(group => group.Items)
				.Where(apiDesc => apiDesc.HttpMethod == "POST")
				.Where(apiDesc => !(options.IgnoreObsoleteActions && apiDesc.CustomAttributes().OfType<ObsoleteAttribute>().Any()));
			//.Where(apiDesc => rpcMethods.Any(m=>m.Name.Equals(apiDesc.ActionDescriptor));
			//.Where(apiDesc => options.DocInclusionPredicate(documentName, apiDesc));
			OpenApiPaths paths = this.GetOpenApiPaths(metaData, schemaRepository, applicableApiDescriptions);

			var doc = new OpenApiDocument()
			{
                Info = info,
                Servers = this.swagerOptions.Endpoints.Select(x => new OpenApiServer()
				{
					Url = x
				}).ToList(),
				Components = new OpenApiComponents()
				{
					Schemas = schemaRepository.Schemas
				},
				Paths = paths
			};
			this.cacheDocument = doc;
			if (!applicableApiDescriptions.Any())
            {
				this.cacheDocument = null;
			}

			return this.cacheDocument;
		}

		private OpenApiPaths GetOpenApiPaths(RpcRouteMetaData metaData, SchemaRepository schemaRepository, IEnumerable<ApiDescription> apiDescriptions)
		{
			OpenApiPaths paths = new OpenApiPaths();

            //foreach (var route in metaData.PathRoutes)
            //{

            //}

			List<UniqueMethod> uniqueMethods = this.GetUniqueKeyMethodPairs(metaData);

			foreach (UniqueMethod method in uniqueMethods)
			{
				string operationKey = method.UniqueUrl.Replace("/", "_").Replace("#", "|");
				var apiDescription = apiDescriptions
					.FirstOrDefault(apiDesc => 
						apiDesc.TryGetMethodInfo(out var methodInfo) &&
						methodInfo.Equals(method.Info.MethodInfo));
				OpenApiOperation operation = this.GetOpenApiOperation(operationKey, method.Info, schemaRepository, apiDescription);

				var pathItem = new OpenApiPathItem()
				{
					Operations = new Dictionary<OperationType, OpenApiOperation>()
					{
						[OperationType.Post] = operation
					}
				};
				paths.Add(method.UniqueUrl, pathItem);
			}

			return paths;
		}

		private OpenApiOperation GetOpenApiOperation(string key, IRpcMethodInfo methodInfo, SchemaRepository schemaRepository, ApiDescription apiDescription)
		{
			string methodAnnotation = this.xmlDocumentationService.GetSummaryForMethod(methodInfo);
			Type trueReturnType = this.GetReturnType(methodInfo.RawReturnType);

			return new OpenApiOperation()
			{
				Tags = GenerateOperationTags(apiDescription),
				OperationId = apiDescription == null ? null : options.OperationIdSelector(apiDescription),
				Parameters = GenerateParameters(apiDescription, schemaRepository),
				//Tags = new List<OpenApiTag>(),
				Summary = methodAnnotation,
				RequestBody = this.GetOpenApiRequestBody(key, methodInfo, schemaRepository),
				Responses = this.GetOpenApiResponses(key, trueReturnType, schemaRepository),
				Deprecated = apiDescription == null ? false : apiDescription.CustomAttributes().OfType<ObsoleteAttribute>().Any()
			};
		}

		private IList<OpenApiTag> GenerateOperationTags(ApiDescription apiDescription)
		{
			return apiDescription is null? new List<OpenApiTag>() : options.TagsSelector(apiDescription)
				.Select(tagName => new OpenApiTag { Name = tagName })
				.ToList();
		}

		private IList<OpenApiParameter> GenerateParameters(ApiDescription apiDescription, SchemaRepository schemaRespository)
		{
			if (apiDescription is null) return null;
			var applicableApiParameters = apiDescription.ParameterDescriptions
				.Where(apiParam =>
				{
					return //(!apiParam.IsFromBody() && !apiParam.IsFromForm()) &&
						(!apiParam.CustomAttributes().OfType<BindNeverAttribute>().Any())
						&& (apiParam.ModelMetadata == null || apiParam.ModelMetadata.IsBindingAllowed);
				});

			return applicableApiParameters
				.Select(apiParam => GenerateParameter(apiParam, schemaRespository))
				.ToList();
		}
		private OpenApiParameter GenerateParameter(
			ApiParameterDescription apiParameter,
			SchemaRepository schemaRepository)
		{
			var name = options.DescribeAllParametersInCamelCase
				? apiParameter.Name
				//? apiParameter.Name.ToCamelCase()
				: apiParameter.Name;

			var location = (apiParameter.Source != null && ParameterLocationMap.ContainsKey(apiParameter.Source))
				? ParameterLocationMap[apiParameter.Source]
				: ParameterLocation.Query;

            var isRequired = apiParameter.IsRequiredParameter();

            //var schema = (apiParameter.ModelMetadata != null)
            //    ? GenerateSchema(
            //        apiParameter.ModelMetadata.ModelType,
            //        schemaRepository,
            //        apiParameter.PropertyInfo(),
            //        apiParameter.ParameterInfo(),
            //        apiParameter.RouteInfo)
            //    : new OpenApiSchema { Type = "string" };

            var parameter = new OpenApiParameter
			{
				Name = name,
				In = location,
				Required = isRequired,
				//Schema = schema
			};

			var filterContext = new ParameterFilterContext(
				apiParameter,
				null,
				schemaRepository,
				apiParameter.PropertyInfo(),
				apiParameter.ParameterInfo());

			foreach (var filter in options.ParameterFilters)
			{
				filter.Apply(parameter, filterContext);
			}

			return parameter;
		}
		private static readonly Dictionary<BindingSource, ParameterLocation> ParameterLocationMap = new Dictionary<BindingSource, ParameterLocation>
		{
			{ BindingSource.Query, ParameterLocation.Query },
			{ BindingSource.Header, ParameterLocation.Header },
			{ BindingSource.Path, ParameterLocation.Path }
		};
		private Type GetReturnType(Type returnType)
		{
			if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				//Return the `Task` return type
				return returnType.GenericTypeArguments.First();
			}
			if (returnType == typeof(Task))
			{
				//Task with no return type
				return typeof(void);
			}
			return returnType;
		}

		private OpenApiResponses GetOpenApiResponses(string key, Type returnMethodType, SchemaRepository schemaRepository)
		{
			return new OpenApiResponses()
			{
				["200"] = new OpenApiResponse()
				{
					Content = new Dictionary<string, OpenApiMediaType>()
					{
						["application/json"] = new OpenApiMediaType
						{
							Schema = this.GeResposeSchema(key, returnMethodType, schemaRepository)
						}
					}
				}
			};
		}

		private OpenApiRequestBody GetOpenApiRequestBody(string key, IRpcMethodInfo methodInfo,
			SchemaRepository schemaRepository)
		{
			return new OpenApiRequestBody()
			{
				Content = new Dictionary<string, OpenApiMediaType>()
				{
					["application/json"] = new OpenApiMediaType()
					{
						Schema = this.GetBodyParamsSchema(key, schemaRepository, methodInfo)
					}
				}
			};
		}

		private OpenApiSchema GetBodyParamsSchema(string key, SchemaRepository schemaRepository, IRpcMethodInfo methodInfo)
		{
			OpenApiSchema paramsObjectSchema = this.GetOpenApiEmptyObject();

			foreach (IRpcParameterInfo parameterInfo in methodInfo.Parameters)
			{
				string name = this.namePolicy.ConvertName(parameterInfo.Name);
				OpenApiSchema schema = this.schemaGenerator.GenerateSchema(parameterInfo.RawType, schemaRepository);
				paramsObjectSchema.Properties.Add(name, schema);
			}

			paramsObjectSchema = schemaRepository.AddDefinition($"{key}", paramsObjectSchema);

			var requestSchema = this.GetOpenApiEmptyObject();

			requestSchema.Properties.Add("id", this.schemaGenerator.GenerateSchema(typeof(string), schemaRepository));
			requestSchema.Properties.Add("jsonrpc", this.schemaGenerator.GenerateSchema(typeof(string), schemaRepository));
			requestSchema.Properties.Add("method", this.schemaGenerator.GenerateSchema(typeof(string), schemaRepository));
			requestSchema.Properties.Add("params", paramsObjectSchema);

			requestSchema = schemaRepository.AddDefinition($"request_{key}", requestSchema);

			this.RewriteJrpcAttributesExamples(requestSchema, schemaRepository, this.namePolicy.ConvertName(methodInfo.Name));

			return requestSchema;
		}

		private OpenApiSchema GeResposeSchema(string key, Type returnMethodType, SchemaRepository schemaRepository)
		{
			var resultSchema = this.schemaGenerator.GenerateSchema(returnMethodType, schemaRepository);

			var responseSchema = this.GetOpenApiEmptyObject();
			responseSchema.Properties.Add("id", this.schemaGenerator.GenerateSchema(typeof(string), schemaRepository));
			responseSchema.Properties.Add("jsonrpc", this.schemaGenerator.GenerateSchema(typeof(string), schemaRepository));
			responseSchema.Properties.Add("result", resultSchema);

			responseSchema = schemaRepository.AddDefinition($"response_{key}", responseSchema);
			this.RewriteJrpcAttributesExamples(responseSchema, schemaRepository);
			return responseSchema;
		}

		private OpenApiSchema GetOpenApiEmptyObject()
		{
			return new OpenApiSchema
			{
				Type = "object",
				Properties = new Dictionary<string, OpenApiSchema>(),
				Required = new SortedSet<string>(),
				AdditionalPropertiesAllowed = false
			};
		}

		private void RewriteJrpcAttributesExamples(OpenApiSchema schema, SchemaRepository schemaRepository, string method = "method_name")
		{
			var jrpcAttributesExample =
				new OpenApiObject()
				{
					{"id", new OpenApiString(Guid.NewGuid().ToString())},
					{"jsonrpc", new OpenApiString("2.0")},
					{"method", new OpenApiString(method)},
				};

			foreach (var prop in schemaRepository.Schemas[schema.Reference.Id].Properties)
			{
				if (jrpcAttributesExample.ContainsKey(prop.Key))
				{
					prop.Value.Example = jrpcAttributesExample[prop.Key];
				}
			}
		}
	}
}