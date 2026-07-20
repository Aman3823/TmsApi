using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
[Tags("Enrollments")]//Scalar ላይ በ Enrollments ግሩፕ እንዲቀመጥ
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status500InternalServerError)]
public class EnrollmentsController(
    ICourseService courseService,
    IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
    [ProducesResponseType(typeof(EnrollmentResponseDto),StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status404NotFound)]
    [EndpointSummary("Get one enrollment for a course")]
    public async Task<IActionResult> GetEnrollment(int courseId, int id, CancellationToken ct)
    {
        var enrollment = await enrollmentService.GetByIdAsync(courseId, id, ct);
        return enrollment is not null ? Ok(enrollment) : NotFound();
    }
      [HttpGet(Name = "ListCourseEnrollments")]
      [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>),StatusCodes.Status200OK)]
      [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status404NotFound)]
      [EndpointSummary("List enrollments for a course")]
public async Task<IActionResult> GetEnrollments(int courseId, CancellationToken ct)
{
    // የኮርሱ መኖር አለመኖር ማረጋገጫ
    var course = await courseService.GetByIdAsync(courseId, ct);
    if (course == null)
    {
        return NotFound();
    }

    // የኢንሮልመንት ዝርዝሩን ማምጫ
    var enrollments = await enrollmentService.GetByCourseAsync(courseId, ct);
    return Ok(enrollments);
}
    [HttpPost]
    [ProducesResponseType(typeof(EnrollmentResponseDto),StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails),StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status409Conflict)]
    [EndpointSummary("Enrol a student in a course")]
    [EndpointDescription("Returns 404 if the course does not exist, 409 if the course has reached MaxCapacity.")]
    public async Task<IActionResult> EnrollStudent(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        // ህግ 1: መጀመሪያ ኮርሱ በጭራሽ መኖሩን አረጋግጥ (404 Not Found)
        var course = await courseService.GetByIdAsync(courseId, ct);
        if (course == null)
        {
            return NotFound();
        }

        // ህግ 2: ኮርሱ ሙሉ መሆን አለመሆኑን አረጋግጥ (409 Conflict)
        if (course.EnrollmentCount >= course.MaxCapacity)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Course is full",
                Detail = $"Course '{course.Title}' has reached its maximum capacity of {course.MaxCapacity}.",
                Status = StatusCodes.Status409Conflict
            });
        }

        // ሁሉንም ህግ ካለፈ ይመዘገባል
        var enrollment = await enrollmentService.CreateAsync(courseId, request, ct);
        return CreatedAtAction(nameof(GetEnrollment), new { courseId, id = enrollment.Id }, enrollment);
    }
}