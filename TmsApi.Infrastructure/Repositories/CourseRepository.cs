using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;
namespace TmsApi.Infrastructure.Repositories;
public class CourseRepository(TmsDbContext dbContext): ICourseRepository
{
    public async Task<Course?> GetByCodeAsync(string code,CancellationToken ct = default)
    {
        return await dbContext.Courses
        .Include(c =>c.Enrollments)
        .FirstOrDefaultAsync(c =>c.Code == code,ct);
    }
}