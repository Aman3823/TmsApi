using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;
using Microsoft.AspNetCore.SignalR;
using TmsApi.Application.Hubs;
using TmsApi.Api.Hubs;
namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/enrollments")]
[ApiVersion("2.0")]
public class EnrollmentsController(IHubContext<TmsHub,ITmsHubClient>hubContext, IMediator mediator) : ControllerBase
{
    // 1. Mock Data የሚያወጣው GetAll
    [HttpGet]
    public IActionResult GetAll()
    {
        var mockEnrollments = new[]
        {
            new { 
                id = "1", 
                studentId = 101, 
                studentName = "Liya Kebede", 
                courseId = 201, 
                courseName = "Angular Deep Dive", 
                status = "Pending", 
                enrolledAt = DateTime.UtcNow.ToString("o") 
            },
            new { 
                id = "2", 
                studentId = 102, 
                studentName = "Aman Bekele", 
                courseId = 202, 
                courseName = ".NET Core Architecture", 
                status = "Pending", 
                enrolledAt = DateTime.UtcNow.ToString("o") 
            }
        };

        return Ok(mockEnrollments);
    }

    // 2. HttpPost የተደረገው Approve (405 ኤረርን ይቀርፋል)
    [HttpPost("{id}/approve")]
    public async Task <IActionResult> Approve(string id)
    
    {
        // 🎯 SignalR Broadcast: ለሁሉም Connected Clients መረጃውን ይልካል
        await hubContext.Clients.All
        .ReceiveEnrollmentStatusUpdated(id,"Approved");
        return Ok();
    }
    
    [HttpPost]
    public async Task<IActionResult> Enroll(
        EnrollStudentCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.Match(
            onSuccess: created => CreatedAtAction(
                nameof(GetSchedule),
                new { studentId = created.StudentId },
                created),
            onFailure: error =>
            {
                var status = error.Code switch
                {
                    "course_not_found" => StatusCodes.Status404NotFound,
                    "course_full" or "already_enrolled" => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest
                };

                return Problem(
                    statusCode: status,
                    title: "Enrollment rejected",
                    detail: error.Message,
                    type: $"https://tms.local/errors/{error.Code}");
            });
    }

    [HttpGet("{studentId}/schedule")]
    public async Task<IActionResult> GetSchedule(
        int studentId, CancellationToken ct)
    {
        var schedule = await mediator.Send(
            new GetStudentScheduleQuery(studentId), ct);

        return Ok(schedule);
    }
}