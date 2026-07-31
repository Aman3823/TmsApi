using Microsoft.AspNetCore.Authentication;
using Asp.Versioning;
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
using TmsApi.Application.Behaviors;
using TmsApi.Api.ExceptionHandlers; // የ Course እና Enrollment ሰርቪሶች እንዲታዩ የተጨመረ

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHybridCache();
builder.Services.AddCors(Options =>
{
    Options.AddPolicy("AllowAngular",policy =>
    policy.WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod());

});

builder.Services.AddApiVersioning(Options=>
{
    Options.DefaultApiVersion = new ApiVersion(1, 0);
    Options.AssumeDefaultVersionWhenUnspecified = true;
    Options.ReportApiVersions = true;
    Options.ApiVersionReader = new UrlSegmentApiVersionReader();
})

.AddApiExplorer(Options =>
{
    Options.GroupNameFormat = "'v'VVV";
    Options.SubstituteApiVersionInUrl =true;
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
// --- 2. Rate Limiting Registration ---
builder.Services.AddRateLimiter(options =>
{
    // Global Tier-Aware Token Bucket
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

    // Concurrency Limiter for expensive endpoints (e.g. Transcripts)
    options.AddConcurrencyLimiter("transcripts", opt =>
    {
        opt.PermitLimit = 5;
        opt.QueueLimit = 20;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Response Config for 429 Rejections (RFC 7807 compliant with Dynamic Retry-After)
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
// Add services to the container.
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
builder.Services.AddControllers();

// Dependency Injection Registration
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
// Repositories
builder.Services.AddScoped<ICourseRepository,CourseRepository>();
builder.Services.AddScoped<IEnrollmentRepository,EnrollmentRepository>();
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<ICourseService, CourseService>();
// MediatR & Validation
builder.Services.AddMediatR(cfg =>
cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);
// Pipeline Behaviors (Logging FIRST, Validation SECOND)
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>),typeof(ValidationBehavior<,>));
// Exception Handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers(Options =>
{
    Options.Filters.Add<AuditLogFilter>();
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
app.UseCors("AllowAngular");
app.UseExceptionHandler(); 
app.UseHttpsRedirection();
app.UseStatusCodePages();

app.MapGet("/api/error", () =>
{
    throw new Exception("Simulated database failure for ProblemDetails testing");
});

app.UseAuthentication();
app.UseAuthorization();

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

    // ዳታቤዙን ማይግሬት ማድረግ
    context.Database.Migrate();
    if(app.Environment.IsDevelopment())
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

        // var courses = new List<Course>
        // {
        //     new() { Code = "CS-101", Title = "Introduction to Computer Science", MaxCapacity = 30 },
        //     new() { Code = "CS-201", Title = "Data Structures and Algorithms", MaxCapacity = 25 },
        //     new() { Code = "MAT-101", Title = "Calculus I", MaxCapacity = 40 }
        // };

        // context.Courses.AddRange(courses);
        // context.SaveChanges();

        // var enrollments = new List<Enrollment>
        // {
        //     new() { StudentId = students[0].Id, CourseId = courses[0].Id, Grade = 4.0m },
        //     new() { StudentId = students[0].Id, CourseId = courses[1].Id, Grade = 3.6m },
        //     new() { StudentId = students[1].Id, CourseId = courses[0].Id, Grade = 2.8m },
        //     new() { StudentId = students[3].Id, CourseId = courses[0].Id, Grade = 3.9m }
        // };

        // context.Enrollments.AddRange(enrollments);
        context.SaveChanges();
    }

    // የ Soft-Delete
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