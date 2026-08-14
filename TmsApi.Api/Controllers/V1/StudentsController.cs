using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using TmsApi.Application.Dtos;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers.V1;

[ApiController]
[Route("api/students")]
[Tags("Students")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class StudentsController(IStudentService studentService, LinkGenerator linkGenerator) : ControllerBase
{
    [HttpGet("{id:int}", Name = nameof(GetStudentById))]
    [ProducesResponseType(typeof(StudentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a student by ID")]
    [EndpointDescription("Returns student details along with HATEOAS navigational links.")]
    public async Task<IActionResult> GetStudentById(int id, CancellationToken ct)
    {
        var student = await studentService.GetByIdAsync(id, ct);
        if (student is null)
        {
            return NotFound();
        }

        var links = new List<LinkDto>
        {
            // 1. "self" Link
            new(linkGenerator.GetPathByName(HttpContext, nameof(GetStudentById), new { id }) ?? $"/api/students/{id}", "self", "GET"),
            
            // 2. "update" Link
            new(linkGenerator.GetPathByName(HttpContext, nameof(GetStudentById), new { id }) ?? $"/api/students/{id}", "update", "PUT"),
            
            // 3. "delete" Link
            new(linkGenerator.GetPathByName(HttpContext, nameof(GetStudentById), new { id }) ?? $"/api/students/{id}", "delete", "DELETE"),
            
            // 4. "enrollments" Link
            new(linkGenerator.GetPathByAction(HttpContext, action: "GetStudentEnrollments", controller: "Enrollments", values: new { studentId = id }) ?? $"/api/students/{id}/enrollments", "enrollments", "GET")
        };

        var detailDto = new StudentDetailDto
        {
            Id = student.Id,
            Name = student.Name,
            RegistrationNumber = student.RegistrationNumber,
            GPA = student.GPA,
            IsActive = student.IsActive,
            Links = links
        };

        return Ok(detailDto);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<StudentResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List students with pagination")]
    [EndpointDescription("Returns a paginated list of students. pageSize is capped at 50.")]
    public async Task<IActionResult> GetStudents([FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await studentService.GetStudentsAsync(request, ct);
        return Ok(result);
    }
}