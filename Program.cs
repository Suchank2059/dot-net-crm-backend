using Crm.Api.Configuration;
using Crm.Api.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Crm.Api.Infrastructure.Exceptions;
using Crm.Api.Common.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Configure automatic model validation responses
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "Invalid value"
                        : e.ErrorMessage)
                    .ToArray()
            );

        return ApiResponses.ValidationFailed(errors);
    };
});


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



// Register exception handling early in the pipeline
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
