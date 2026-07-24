using TmsApi.Application.Dtos;
namespace TmsApi.Application.Interfaces;

public interface IEnrollmentService
{
    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct);
    Task<IEnumerable<EnrollmentResponseDto>> GetAllAsync(CancellationToken ct = default);
   Task<List<EnrollmentResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct);
}