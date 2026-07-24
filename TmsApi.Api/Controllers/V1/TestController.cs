using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Dtos; 
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Controllers.V1;

[ApiController]
[Route("api/test")]
public class TestController(TmsDbContext context) : ControllerBase
{
    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine("\n>>> STEP 1: Building the query object (no database contact)...");

        var query = context.Students.Where(s => s.GPA >= 3.0m);

        Console.WriteLine(">>> STEP 2: Appending a sorting clause...");

        var orderedQuery = query.OrderBy(s => s.Name);

        Console.WriteLine(">>> STEP 3: Materializing query into a C# List...");

        var results = orderedQuery.ToList();

        Console.WriteLine(">>> STEP 4: Materialization finished. List populated.\n");

        return Ok(results);
    }

    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }

    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine("\n>>> STEP 1: Running non-translatable query...");

        try
        {
            var students = context.Students
                .Where(s => IsHonorRoll(s.GPA))
                .ToList();

            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> EXCEPTION CAUGHT: {ex.Message}\n");

            return BadRequest(new
            {
                Message = ex.Message
            });
        }
    }

    [HttpGet("count")]
    public async Task<IActionResult> CountStudents()
    {
        var count = await context.Students
            .Where(s => s.IsActive && s.GPA >= 3.0m)
            .CountAsync();

        return Ok(new
        {
            Count = count
        });
    }

    [HttpGet("courses-most-enrollments")]
    public async Task<IActionResult> CoursesMostEnrollments()
    {
        var list = await context.Courses
            .Select(c => new
            {
                c.Title,
                EnrollmentCount = c.Enrollments.Count
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("average-gpa-per-course")]
    public async Task<IActionResult> AverageGpaPerCourse()
    {
        var list = await context.Enrollments
            .GroupBy(e => e.Course.Title)
            .Select(g => new
            {
                Course = g.Key,
                AverageGPA = g.Average(e => e.Student.GPA)
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("students-no-enrollments")]
    public async Task<IActionResult> StudentsNoEnrollments()
    {
        var list = await context.Students
            .Where(s => !s.Enrollments.Any())
            .Select(s => s.Name)
            .ToListAsync();

        return Ok(list);
    }
   [HttpGet("students")]
public async Task<IActionResult> GetPagedStudents(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
{
    if (page < 1)
        page = 1;

    Console.WriteLine(
        $"\n>>> FETCHING STUDENTS: Page {page}, Size {pageSize} <<<");

    var students = await context.Students
        .OrderBy(s => s.Name)
        .ThenBy(s => s.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return Ok(students);
}
[HttpGet("courses")]
public async Task<IActionResult> GetCourses(
    CancellationToken cancellationToken = default)
{
    Console.WriteLine(
        "\n>>> GENERATING COURSES REPORT (GROUP BY) <<<");

    var courses = await context.Enrollments
        .GroupBy(e => e.Course.Id)
        .Select(g => new
        {
            CourseId = g.Key,
            EnrollmentCount = g.Count()
        })
        .OrderByDescending(c => c.EnrollmentCount)
        .Take(5)
        .ToListAsync(cancellationToken);

    return Ok(courses);
    
}
[HttpGet("n-plus-one-trap")]
public async Task<IActionResult>TestNPlusOneTrap(CancellationToken cancellationToken)
{
    Console.WriteLine("\n>>> STARTING N +1 TRAP EXPERIMENT (1+ N Queries) <<<");
    var Students = await context.Students.AsNoTracking().ToListAsync(cancellationToken);
    var reportResult = new List<object>();
    foreach( var s in Students)
    {
      var count = await context.Enrollments
      .AsNoTracking()
      .CountAsync(e => e.StudentId ==s.Id, cancellationToken);
      Console.WriteLine($"[LOG] Student {s.Name} has {count} Enrollments.");
      reportResult.Add(new {StudentName = s.Name, EnrollmentCount = count});

    }
    Console.WriteLine(">>> N + 1 EXPERIMENT FINISHED \n");
    return Ok(reportResult);

}
[HttpGet("n-plus-one-fixed")]
public async Task<IActionResult> TestNPlusOneFixed(CancellationToken cancellationToken)
{
    Console.WriteLine("\n>>> RUNNING FIXED EAGER SHAPED QUERY(1 Single Query)<<<");
    var report = await context.Students
    .AsNoTracking()
    .Select(s => new 
    {
        StudentName =s.Name,
        EnrollmentCount =s.Enrollments.Count

    })
    .ToListAsync(cancellationToken);
    foreach( var r in report)
    {
        Console.WriteLine($"[LOG - FIXED] Student {r.StudentName} has {r.EnrollmentCount} enrollments.");

    }
    Console.WriteLine(">>> FIXED EXPERIMENT FINISHED \n");
    return Ok(report);
}
}
