
using API.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using SaaS.Api.Configuration;
using SaaS.Api.Extensions;
using SaaS.Api.Middlewares;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Settings;
using SaaS.Application.Features.Users.Queries.GetUserById;
using SaaS.Domain.Entities;
using SaaS.Infrastructure;
using SaaS.Infrastructure.DataSeeding;
using SaaS.Infrastructure.Extensions;
using SaaS.Infrastructure.Hubs;
using System.Data;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddInfrastructure(connectionString ?? "");

builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection(SecuritySettings.SectionName));
builder.Services.Configure<N8nOptions>(builder.Configuration.GetSection(N8nOptions.SectionName));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.Configure<Dictionary<string, string>>(builder.Configuration.GetSection("N8nWebhooks"));
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection(GeneralSettings.SectionName));
builder.Services.Configure<JobWatchdogOptions>(builder.Configuration.GetSection(JobWatchdogOptions.SectionName));

// 2. Extract values locally to configure JWT during application startup
var securitySettings = new SecuritySettings();
builder.Configuration.GetSection(SecuritySettings.SectionName).Bind(securitySettings);

// 3. Register JWT passing the object directly
builder.Services.AddJwtAuthentication(securitySettings);

// prevent infinite loop when serializing objects with circular refrences 
builder.Services.AddControllers().AddJsonOptions(x =>
{
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddMediatRWithValidation();

builder.Services.AddFrontendCors(builder.Configuration, builder.Environment);

var app = builder.Build();

#region DataSeeding
try
{
    using (var scope = app.Services.CreateScope())
    {
        var DbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        await UsersSeeding.SeedingAsync(DbContext, passwordHasher, httpContextAccessor);
        await BotsSeeding.SeedingAsync(DbContext);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An Error Occurs When Applying The Migrations");
}
#endregion

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors(FrontendCorsSettings.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<SingleActiveSessionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    options.RoutePrefix = ""; // Serve Swagger UI at the app's root
});

app.MapControllers();

app.MapHub<AppNotificationHub>("/hubs/notifications");

app.Run();
