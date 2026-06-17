using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddProblemDetails();
builder.Services.AddAuthorization();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddControllers();
builder.Services
    .AddScoped<IEnrollmentService,
               EnrollmentService>();

builder.Services
    .AddSingleton<EnrollmentWorker>();

builder.Services
    .AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Host.UseDefaultServiceProvider(options =>
{
options.ValidateScopes = true;
options.ValidateOnBuild = true;
});

var app = builder.Build();
if(app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else {
    app.UseExceptionHandler();
}
app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapGet("/api/error", () =>
{
throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

app.UseAuthentication();
app.UseAuthorization();
app.MapGet(
"/api/enrollments/worker-smoke",
(EnrollmentWorker worker) =>
{
    worker.ProcessBatch();

    return Results.Ok("processed");
    
});

app.MapControllers();

app.Run();