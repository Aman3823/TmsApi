using MediatR;

namespace TmsApi.Application.Courses.Commands.UpdateCourse;

public record UpdateCourseCommand(
    Guid Id,
    string Title,
    string Code,
    int MaxCapacity
) : IRequest<bool>;