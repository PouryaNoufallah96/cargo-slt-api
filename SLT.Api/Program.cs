using Autofac;
using Autofac.Extensions.DependencyInjection;
using SLT.Api.Utilities.Configurations;
using SLT.Api.Utilities.Middlewares;
using SLT.Services._Price._Hubs;
using SLT.Services._TransactionLog._Hub;
using System.Text.Json.Serialization;
using Utilities.Configuration;

var builder = WebApplication.CreateBuilder(args);



builder.Services.AddCustomControllers();

builder.Services.AddCustomApiVersioning();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger();
builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();

builder.Services.AddCodeAssistantSettings(builder.Configuration);
builder.Services.AddSettings(builder.Configuration);



builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(autofacConfigure =>
{
    autofacConfigure.AddServices();
    autofacConfigure.AddControllerServices();

});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters
       .Add(new JsonStringEnumConverter());
});

builder.WebHost.UseSentry(o =>
{
    o.Dsn = "https://e8dd66209d116e6ea99535cf7b48c071@o4510492345368576.ingest.de.sentry.io/4510815780667472";
    o.TracesSampleRate = 0;
    o.AttachStacktrace = true;
    o.SendDefaultPii = false;
    o.Debug = false;
    o.IncludeActivityData = false;
});


var app = builder.Build();

app.UseHsts(app.Environment);

app.UseDeveloperExceptionPage(app.Environment);

app.UseSwaggerAndUI();

app.UseRequestLogger();

app.UseCustomExceptionHandler();

app.UseJWTBlackList();


app.UseProductionCors();

app.UseFirewall();

app.UseSignature();

app.UseJwt();

app.UseRouting();

app.UseCustomRateLimiting();

app.UseAuthorization();

app.UseEndpoints();


app.MapHub<PriceHub>("/hubs/prices");
app.MapHub<WalletNotifyHub>("/hubs/NotifyWallet");

app.Run();