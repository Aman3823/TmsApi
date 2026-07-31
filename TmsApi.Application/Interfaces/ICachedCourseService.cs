using TmsApi.Application.Dtos;

namespace TmsApi.Application.Interfaces;

public interface ICachedCourseService
{
    // CourseResponseDto መሆኗን አረጋግጥ
    Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    
    // PagedResponse<CourseResponseDto> (ወይም ICourseService የሚመልሰውን ዓይነት) አድርገው
    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct);
    
    Task InvalidateCourseCacheAsync(CancellationToken ct);
}