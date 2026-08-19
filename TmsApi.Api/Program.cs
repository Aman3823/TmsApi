using Microsoft.AspNetCore.Authentication;
using Asp.Versioning;
using TmsApi.Application.Notifications;
using TmsApi.Api.Notifications;
using TmsApi.Infrastructure.Transcripts;
using System.Threading.Channels;
using TmsApi.Application.Transcripts;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Hybrid;
using TmsApi.Api.RateLimiting;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Services;
using TmsApi.Api.Filters;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using TmsApi.Infrastructure.Repositories;
using TmsApi.Application.Enrollments.Commands;
using FluentValidation;
using MediatR;
using TmsApi.Infrastructure.Workers;
using TmsApi.Application.Behaviors;
using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Hubs;
using Microsoft.AspNetCore.Antiforgery;
var builder = WebApplication.CreateBuilder(args);

// Load allowed origins from appsettings.Development.json
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:4200" };
builder.Services.AddAntiforgery(Options =>
{
  Options.HeaderName ="X-XSRF-TOKEN"  ;
});
// Register the named CORS policy "TmsClient"
builder.Services.AddCors(options =>
{
    options.AddPolicy("TmsClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials() // Vital for HttpOnly cookies & SignalR
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

builder.Services.AddHybridCache();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ITranscriptNotificationService, SignalRTranscriptNotificationService>();
builder.Services.AddSingleton(Channel.CreateBounded<TranscriptRequest>(new BoundedChannelOptions(100)
{
    FullMode = BoundedChannelFullMode.Wait
}));

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(2, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

// --- Rate Limiting Registration ---
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var (partitionKey, tier) = ApiKeyResolver.Resolve(httpContext);

        return tier switch
        {
            ApiKeyTier.Paid => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"paid:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 200,
                    TokensPerPeriod = 100,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }),
            ApiKeyTier.Free => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"free:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 30,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }),
            _ => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"anon:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 5,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                })
        };
    });

    options.AddConcurrencyLimiter("transcripts", opt =>
    {
        opt.PermitLimit = 5;
        opt.QueueLimit = 20;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        var retryAfter = "10";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ts))
        {
            retryAfter = ((int)ts.TotalSeconds).ToString();
        }

        context.HttpContext.Response.Headers.RetryAfter = retryAfter;
        context.HttpContext.Response.ContentType = "application/problem+json";

        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "Rate limit exceeded",
            Detail = $"Too many requests. Retry after {retryAfter} seconds.",
            Status = StatusCodes.Status429TooManyRequests,
            Type = "https://tms.local/errors/rate_limit_exceeded"
        }, ct);
    };
});

builder.Services.AddOpenApi();

builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

// Database Connection
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
           .LogTo(Console.WriteLine, LogLevel.Information)
           .EnableSensitiveDataLogging()
);

builder.Services.AddProblemDetails();
builder.Services.AddAuthorization();

// Dependency Injection Registration
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddSingleton<EnrollmentWorker>();

builder.Services.AddSingleton<ITranscriptStatusStore, InMemoryTranscriptStatusStore>();
builder.Services.AddHostedService<TranscriptWorker>();

// MediatR & Validation
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);

// Pipeline Behaviors
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Exception Handling & Controllers
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

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

// --- HTTP Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler(); 
app.UseHttpsRedirection();
app.UseStatusCodePages();

// CORS is registered before Auth and RateLimiter
app.UseCors("TmsClient");

app.UseRateLimiter();
app.MapHub<TmsHub>("/hubs/tms");
app.UseAuthentication();
app.UseAuthorization();
// 🛡️ XSRF Token Cookie አዘጋጅቶ ለ Angular መላኪያ Middleware
app.Use(async(context,next)=>
{
   // ተጠቃሚው Authenitcated ከሆነ ወይም "tms_auth" Cookie ካለው
   if (context.User.Identity?.IsAuthenticated == true || context.Request.Cookies.ContainsKey("tms_auth"))
    {
       var antiforgery =context.RequestServices.GetRequiredService<IAntiforgery>();
       var tokens =antiforgery.GetAndStoreTokens(context);
       context.Response.Cookies.Append("XSRF-TOKEN",tokens.RequestToken!,
       new CookieOptions
       {
           HttpOnly =false, // 👈 Angular በ JavaScript አውጥቶ በ Header እንዲልከው false መሆን አለበት!
           Secure =!builder.Environment.IsDevelopment(),
           SameSite =SameSiteMode.Strict
       }) ;

    }
    await next(context);
});
app.MapGet("/api/error", () =>
{
    throw new Exception("Simulated database failure for ProblemDetails testing");
});

app.MapGet("/api/enrollments/worker-smoke", (EnrollmentWorker worker) =>
{
    worker.ProcessBatch();
    return Results.Ok("processed");
});

app.UseMiddleware<TmsApi.Api.Middleware.V1DeprecationMiddleware>();
app.MapControllers();

// Database Migration, Seeding and Soft-Delete Testing
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();

    context.Database.Migrate();
    if (app.Environment.IsDevelopment())
    {
        await DataSeeder.SeedAsync(context);
    }

    if (!context.Students.Any())
    {
        var students = new List<Student>
        {
            new() { RegistrationNumber = "TMS-2026-0001", Name = "Alice Smith", GPA = 3.8m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0006", Name = "foziya", GPA = 2.5m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0002", Name = "Bob Jones", GPA = 2.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0003", Name = "Charlie Brown", GPA = 3.4m, IsActive = false },
            new() { RegistrationNumber = "TMS-2026-0004", Name = "Diana Prince", GPA = 3.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0005", Name = "Evan Wright", GPA = 2.5m, IsActive = true }
        };

        context.Students.AddRange(students);
        context.SaveChanges();
    }

    Console.WriteLine("====== Soft-Delete ======");
    var studentToTest = await context.Students.FirstOrDefaultAsync();
    
    if (studentToTest != null)
    {
        studentToTest.IsDeleted = true;
        await context.SaveChangesAsync();
        Console.WriteLine($"{studentToTest.Name} soft-deleted");
        
        var normalQueryStudent = await context.Students
            .FirstOrDefaultAsync(s => s.Id == studentToTest.Id);
        Console.WriteLine($" Normal user found: {(normalQueryStudent == null ? "No (Hidden)" : "Found it")}");
        
        var adminQueryStudent = await context.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == studentToTest.Id);
        Console.WriteLine($" Admin found: {(adminQueryStudent != null ? "Found it (Can view)" : "No")}");
    }
    else
    {
        Console.WriteLine("Test student data not found in the database!");
    }
    Console.WriteLine("=======================================");   
}

app.Run();