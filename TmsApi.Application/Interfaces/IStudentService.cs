using TmsApi.Application.Dtos;

namespace TmsApi.Application.Interfaces;

public interface IStudentService
{
    Task<StudentDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResponse<StudentResponseDto>> GetStudentsAsync(PagedRequest request, CancellationToken ct = default);
}