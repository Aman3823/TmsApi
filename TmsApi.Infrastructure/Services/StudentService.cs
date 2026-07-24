using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Dtos;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;
namespace TmsApi.Infrastructure.Services;

public class StudentService(TmsDbContext dbContext) : IStudentService
{
    public async Task<StudentDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var student = await dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (student is null) return null;

        return new StudentDetailDto
        {
            Id = student.Id,
            Name = student.Name,
            RegistrationNumber = student.RegistrationNumber,
            GPA = (double)student.GPA,
            IsActive = student.IsActive
        };
    }

    public async Task<PagedResponse<StudentResponseDto>> GetStudentsAsync(PagedRequest request, CancellationToken ct = default)
    {
        var query = dbContext.Students.AsNoTracking();

        var totalCount = await query.CountAsync(ct);

        var students = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new StudentResponseDto
            {
                Id = s.Id,
                Name = s.Name,
                RegistrationNumber = s.RegistrationNumber,
                GPA = (double)s.GPA,
                IsActive = s.IsActive
            })
            .ToListAsync(ct);

        return new PagedResponse<StudentResponseDto>
        {
            Items = students,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }
}