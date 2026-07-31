using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Dtos;
using TmsApi.Application.Interfaces;

namespace TmsApi.Infrastructure.Services;

public class CachedCourseService(
    ICourseService courseService,
    HybridCache cache,
    ILogger<CachedCourseService> logger) : ICachedCourseService
{
    // 1. Get Courses with Caching
    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct)
    {
        var cacheKey = $"courses-page-{request.Page}-size-{request.PageSize}";
        var isDbHit = false;

        var result = await cache.GetOrCreateAsync(
            cacheKey,
            async token =>
            {
                isDbHit = true;
                logger.LogWarning("========== 📦 [CACHE MISS] Fetching Courses from DB ==========");
                return await courseService.GetCoursesAsync(request, token);
            },
            cancellationToken: ct);

        if (!isDbHit)
        {
            logger.LogWarning("========== ⚡ [CACHE HIT] Courses retrieved from HybridCache! ==========");
        }

        return result;
    }

    // 2. Get Single Course By ID
    public async Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var cacheKey = $"course-id-{id}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            async token => await courseService.GetByIdAsync(id, token),
            cancellationToken: ct);
    }

    // 3. Create Course (Invalidates Cache!)
    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var result = await courseService.CreateAsync(request, ct);
        await InvalidateCourseCacheAsync(ct);
        return result;
    }

    // 4. Check Code Exists
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct)
    {
        return courseService.CodeExistsAsync(code, ct);
    }

    // 5. Invalidate Cache
    public async Task InvalidateCourseCacheAsync(CancellationToken ct)
    {
        logger.LogInformation("Clearing Course Caching Tag...");
        await cache.RemoveByTagAsync("courses-tag", ct);
    }
}