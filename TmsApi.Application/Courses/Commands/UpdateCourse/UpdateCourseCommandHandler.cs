using MediatR;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Courses.Commands.UpdateCourse;

public class UpdateCourseCommandHandler(
    ICourseService service,
    ICachedCourseService cachedService)
    : IRequestHandler<UpdateCourseCommand, bool>
{
    public async Task<bool> Handle(UpdateCourseCommand command, CancellationToken ct)
    {
        var result = await service.UpdateAsync(command, ct);

        if (result)
        {
            await cachedService.InvalidateCourseCacheAsync(ct);
        }

        return result;
    }
}