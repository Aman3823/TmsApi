using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

// Database Connection
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDb"))
           .LogTo(Console.WriteLine, LogLevel.Information)
           .EnableSensitiveDataLogging()
);
builder.Services.AddProblemDetails();
builder.Services.AddAuthorization();
builder.Services.AddControllers();

// Dependency Injection Registration
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddSingleton<EnrollmentWorker>();

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
else 
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseStatusCodePages();

app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/enrollments/worker-smoke", (EnrollmentWorker worker) =>
{
    worker.ProcessBatch();
    return Results.Ok("processed");
});

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();

    context.Database.Migrate();

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

        var courses = new List<Course>
        {
            new() { Code = "CS-101", Title = "Introduction to Computer Science", MaxCapacity = 30 },
            new() { Code = "CS-201", Title = "Data Structures and Algorithms", MaxCapacity = 25 },
            new() { Code = "MAT-101", Title = "Calculus I", MaxCapacity = 40 }
        };

        context.Courses.AddRange(courses);

        context.SaveChanges();

        var enrollments = new List<Enrollment>
        {
            new() { StudentId = students[0].Id, CourseId = courses[0].Id, Grade = 4.0m },
            new() { StudentId = students[0].Id, CourseId = courses[1].Id, Grade = 3.6m },
            new() { StudentId = students[1].Id, CourseId = courses[0].Id, Grade = 2.8m },
            new() { StudentId = students[3].Id, CourseId = courses[0].Id, Grade = 3.9m }
        };

        context.Enrollments.AddRange(enrollments);

        context.SaveChanges();
    }
    using (var Testscope = app.Services.CreateScope())
    {
        var db =scope.ServiceProvider.GetRequiredService<TmsApi.Data.TmsDbContext>();
        Console.WriteLine("====== Soft-Delete ======");
        var Student= await db.Students.FirstOrDefaultAsync();
        if (Student != null)
        {
            Student.IsDeleted = true;
            await db.SaveChangesAsync();
            Console.WriteLine($"{Student.Name} soft-deleted");
            var normalQueryStudent = await db.Students
            .FirstOrDefaultAsync(s =>s.Id == Student.Id);
           Console.WriteLine($" Normal user found: {(normalQueryStudent == null ? "No (Hidden)" : "Found it")}");
           var adminQueryStudent = await db.Students
           .IgnoreQueryFilters()
           .FirstOrDefaultAsync(s => s.Id == Student.Id);
          Console.WriteLine($" Admin found: {(adminQueryStudent != null ? "Found it (Can view)" : "No")}");


        }
        
        
         else
    {
        Console.WriteLine("Test student data not found in the database!");

    }

    Console.WriteLine("=======================================");   
        
        
    


        
app.Run();
    }
}