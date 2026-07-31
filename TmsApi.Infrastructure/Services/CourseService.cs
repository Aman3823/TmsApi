using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Courses.Commands.UpdateCourse;
using TmsApi.Application.Dtos;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class CourseService(TmsDbContext context, ILogger<CourseService> logger) : ICourseService
{
    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request, CancellationToken ct)
    {
        IQueryable<Course> query = context.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(c => EF.Functions.ILike(c.Title, $"{request.Search}%")
                                  || EF.Functions.ILike(c.Code, $"%{request.Search}%"));
        }

        var totalCount = await query.CountAsync(ct);

        string orderBy = request.OrderBY?.Trim() switch
        {
            "Code" => "Code",
            "MaxCapacity" => "MaxCapacity",
            _ => "Title"
        };

        IQueryable<Course> sortedQuery = request.Descending switch
        {
            true => orderBy switch
            {
                "Code" => query.OrderByDescending(c => c.Code),
                "MaxCapacity" => query.OrderByDescending(c => c.MaxCapacity),
                _ => query.OrderByDescending(c => c.Title)
            },
            false => orderBy switch
            {
                "Code" => query.OrderBy(c => c.Code),
                "MaxCapacity" => query.OrderBy(c => c.MaxCapacity),
                _ => query.OrderBy(c => c.Title)
            }
        };

        var items = await sortedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count
            ))
            .ToListAsync(ct);

        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<bool> UpdateAsync(UpdateCourseCommand command, CancellationToken ct)
    {
        await Task.CompletedTask;
        return true;
    }

    public async Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity
        };

        context.Courses.Add(course);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Created course {CourseId} ({Code})", course.Id, course.Code);

        return (await GetByIdAsync(course.Id, ct))!;
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        await context.Courses.AsNoTracking().AnyAsync(c => c.Code == code, ct);
        public async Task<CourseResponseDto?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return await context.Courses
            .AsNoTracking()
            .Where(c => c.Code == code)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CourseResponseDto>> GetAllAsync(CancellationToken ct)
    {
        return await context.Courses
            .AsNoTracking()
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count))
            .ToListAsync(ct);
}
}
