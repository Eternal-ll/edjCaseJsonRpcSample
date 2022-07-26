using EdjCase.JsonRpc.Router.Swagger;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using WebApplication3;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
services
    .AddTransient<SwaggerGenerator>()
    .AddSingleton<JsonRpcSwaggerProvider>()
    //enable xml documentation generation in project options (release and debug) for JSON-RPC
    .AddSingleton<IXmlDocumentationService, XmlDocumentationService>()
    .AddTransient<ISwaggerProvider, SwaggerProviderExtended>()
    .AddSwaggerGen(c =>
    {
        c.TagActionsBy(s =>
        {
            var displayName = s.CustomAttributes().OfType<DisplayNameAttribute>().FirstOrDefault();
            var tag = displayName?.DisplayName ?? s.ActionDescriptor.RouteValues["controller"];
            return new List<string>() { tag ?? "default" };
        });
        c.DocInclusionPredicate((docName, apiDesc) => apiDesc.GroupName == docName);
        c.SwaggerDoc("jsonrpc", new OpenApiInfo
        {
            Title = "JSON-RPC Core API",
            Version = "v1",
            Description = "JSON-RPC API",
        });
        c.SwaggerDoc("json", new OpenApiInfo()
        {
            Version = "v1",
            Title = "REST JSON Core API"
        });
        c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{builder.Environment.ApplicationName}.xml"));
    })
    .AddSingleton<ISerializerDataContractResolver>(s =>
        new JsonSerializerDataContractResolver(new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
        })
    )
    .AddJsonRpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/jsonrpc/swagger.json", "JSON-RPC API");
        c.SwaggerEndpoint("/swagger/json/swagger.json", "JSON API");
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.UseEndpoints(c => c.MapControllers());
app.UseJsonRpc();

app.Run();
