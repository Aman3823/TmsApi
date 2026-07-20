using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses")]
[Tags("Courses")]// Scalar ላይ በ Courses ግሩፕ እንዲቀመጥ ያደርጋል
[Produces("application/json")]// የውጤቱን ዓይነት ይገልጻል
[ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status500InternalServerError)]// የ 500 ስህተት ማሳያ
public class CoursesController(ICourseService courseService , LinkGenerator linkGenerator) : ControllerBase
{
    
    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
    [ProducesResponseType(typeof(CourseDetailDto),StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a course by ID")]
    [EndpointDescription("Returns course details with HATEOAS links.Returns 404 if the course does not exist.")]

    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(id, ct);
        if (course is null)
        {
            return  NotFound();
        } 
        var links = new List<LinkDto>();
        //1. "self" Link (GET /api/courses/{id})
        var selfHref = linkGenerator.GetPathByName(
            HttpContext, nameof(GetCourseById), 
            new {id});
            links.Add(new LinkDto(selfHref ?? $"/api/courses/{id}", "self", "GET"));
            // 2. "update" Link (PUT /api/courses/{id})
            var updateHref = linkGenerator.GetPathByName(
                HttpContext, nameof(GetCourseById),
                new{id});
                links.Add(new LinkDto(updateHref ?? $"/api/courses/{id}", "update", "PUT"));
                // 3. "delete" Link (DELETE /api/courses/{id})
                var deleteHref = linkGenerator.GetPathByName(
                    HttpContext, nameof(GetCourseById),
                    new {id});
                    links.Add(new LinkDto(deleteHref ?? $"/api/courses/{id}","delete", "DELETE"));
                    // 4. "enrollments" Link (GET /api/courses/{id}/enrollments)
                    var enrollmentsHref = linkGenerator.GetPathByAction(
                        HttpContext,
                        action: "GetEnrollments",
                        controller: "Enrollments",
                        values: new {courseId = id});
                        links.Add (new LinkDto(enrollmentsHref ?? $"/api/courses/{id}/enrollments", "enrollments" ,"GET"));
                        // 5. "enroll" Conditional Link (POST /api/courses/{id}/enrollments)
        // የኮርሱ አቅም ካልሞላ ብቻ ሊንኩ ይጨመራል
        if(course.EnrollmentCount < course.MaxCapacity)
                             {
                         var enrollHref =linkGenerator.GetPathByAction(
                            HttpContext,
                            action: "CreateEnrollment",
                            controller: "Enrollments",
                            values : new {courseId = id});
                            links.Add (new LinkDto(enrollHref ?? $"/api/courses/{id}/enrollments","enroll", "POST"));
                               }
                            var detailDto = new CourseDetailDto
                            {
                                Id = course.Id,
                                Code = course.Code,
                                Title = course.Title,
                                MaxCapacity = course.MaxCapacity,
                                EnrollmentCount = course.EnrollmentCount,
                                Links = links
                            };
                            return Ok(detailDto);      
                            }   
                             [HttpPost]
                             [ProducesResponseType(typeof(CourseResponseDto),StatusCodes.Status201Created)]
                             [ProducesResponseType(typeof(ValidationProblemDetails),StatusCodes.Status400BadRequest)]
                             [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status409Conflict)]
                             [EndpointSummary("Create a new course")]
                             [EndpointDescription("Creates a course with a unique code. Returns 409 if the course code already exists.")]
    public async Task<IActionResult> CreateCourse(CreateCourseRequest request, CancellationToken ct)
    {
        // እዚህ ጋ በትንሽ 'c' መሆኑን አረጋግጥ
        if (await courseService.CodeExistsAsync(request.Code, ct))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Course code already exists",
                Detail = $"A course with code '{request.Code}' is already registered.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var result = await courseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
        
    }
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CourseResponseDto>),StatusCodes.Status200OK)]
    [EndpointSummary("List course with pagination")]
    [EndpointDescription("Returns a paginated,optionally filtered list of TMS courses. pageSize is capped at 50.")]

    public async Task<IActionResult> GetCourses(
        [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await courseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }

    
    
}