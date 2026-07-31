using MediatR;
namespace TmsApi.Application.Enrollments.Queries;
public record GetStudentScheduleQuery(int StudentId):IRequest<ScheduleDto>;
public record ScheduleDto(int StudentId, List<ScheduleItemDto>Course);
public record ScheduleItemDto(string CourseCode, string Title, string Schedule);