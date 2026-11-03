using Crm.Api.Configuration;
using Crm.Api.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDbContext<ApplicationDbContext>(options =>
        options.UseMySQL(
            builder.Configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")
        ));

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.SecretKey),
        "JWT SecretKey is required.")
    .ValidateOnStart();


var app = builder.Build();



app.MapGet("/", () => "Hello World!");
app.Run();
