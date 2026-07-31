using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Asp.Versioning;
using TmsApi.Application.Dtos; 
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers.V2;

[ApiVersion("2.0")]
[ApiController]
[Route("api/v{version:apiVersion}/courses")]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
// 1. ICourseService የነበረውን ወደ ICachedCourseService ቀይረው:
public class CoursesController(ICachedCourseService courseService, LinkGenerator linkGenerator) : ControllerBase
{
    [HttpGet("{id:int}", Name = "GetCourseById_V2")]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a course by ID (V2)")]
    [EndpointDescription("Returns course details with V2 HATEOAS links.")]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(id, ct);
        if (course is null)
        {
            return NotFound();
        }

        var links = new List<LinkDto>();

        // 1. "self" Link
        var selfHref = linkGenerator.GetPathByName(HttpContext, "GetCourseById_V2", new { id });
        links.Add(new LinkDto(selfHref ?? $"/api/v2/courses/{id}", "self", "GET"));

        // 2. "update" Link
        var updateHref = linkGenerator.GetPathByName(HttpContext, "GetCourseById_V2", new { id });
        links.Add(new LinkDto(updateHref ?? $"/api/v2/courses/{id}", "update", "PUT"));

        // 3. "delete" Link
        var deleteHref = linkGenerator.GetPathByName(HttpContext, "GetCourseById_V2", new { id });
        links.Add(new LinkDto(deleteHref ?? $"/api/v2/courses/{id}", "delete", "DELETE"));

        // 4. "enrollments" Link
        var enrollmentsHref = linkGenerator.GetPathByAction(
            HttpContext,
            action: "GetEnrollments",
            controller: "Enrollments",
            values: new { courseId = id });
        links.Add(new LinkDto(enrollmentsHref ?? $"/api/v2/courses/{id}/enrollments", "enrollments", "GET"));

        // 5. "enroll" Conditional Link
        if (course.EnrollmentCount < course.MaxCapacity)
        {
            var enrollHref = linkGenerator.GetPathByAction(
                HttpContext,
                action: "CreateEnrollment",
                controller: "Enrollments",
                values: new { courseId = id });
            links.Add(new LinkDto(enrollHref ?? $"/api/v2/courses/{id}/enrollments", "enroll", "POST"));
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
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new course")]
    [EndpointDescription("Creates a course with a unique code. Returns 409 if the course code already exists.")]
    public async Task<IActionResult> CreateCourse(CreateCourseRequest request, CancellationToken ct)
    {
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
        return CreatedAtAction("GetCourseById_V2", new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EndpointSummary("List course with pagination (Envelope Standard)")]
    [EndpointDescription("Returns data, meta, and links envelope structure for V2.")]
    public async Task<IActionResult> GetCourses([FromQuery] PagedRequest request, CancellationToken ct)
    {
        // 2. አሁን በ ICachedCourseService በኩል ያመጣል:
        var result = await courseService.GetCoursesAsync(request, ct);

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Clamp(request.PageSize, 1, 50);
        
        var totalPages = result.TotalPages;
        var hasNext = result.HasNext;
        var hasPrevious = result.HasPrevious;

        return Ok(new
        {
            data = result.Items,
            meta = new
            {
                totalCount = result.TotalCount,
                page,
                pageSize,
                totalPages,
                hasNext,
                hasPrevious
            },
            links = new
            {
                self = $"/api/v2/courses?page={page}&pageSize={pageSize}",
                next = hasNext ? $"/api/v2/courses?page={page + 1}&pageSize={pageSize}" : (string?)null,
                prev = hasPrevious ? $"/api/v2/courses?page={page - 1}&pageSize={pageSize}" : (string?)null,
                enroll = "/api/v2/enrollments"
            }
        });
    }
}