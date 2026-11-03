using Crm.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.SecretKey),
        "JWT SecretKey is required.")
    .ValidateOnStart();

app.MapGet("/", () => "Hello World!");
app.Run();
