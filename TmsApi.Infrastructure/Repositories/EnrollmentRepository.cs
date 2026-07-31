using System.Security.AccessControl;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Repositories;

public class EnrollmentRepository(TmsDbContext dbContext) : IEnrollmentRepository
{
    public async Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct= default)
    {
        return await dbContext.Enrollments
        .AnyAsync(e =>e.StudentId == studentId && e.Course.Code == courseCode,ct);
    }
 public async Task AddAsync(Enrollment enrollment, CancellationToken ct = default)
    {
        await dbContext.Enrollments.AddAsync(enrollment, ct);
        await dbContext.SaveChangesAsync(ct);
    }
    public async Task<List<Enrollment>> GetByStudentIdAsync(int studentId, CancellationToken ct = default)
    {
       return await dbContext.Enrollments
       .Include(e =>e.Course)
       .Where(e =>e.StudentId == studentId)
       .ToListAsync(ct); 
    }
}