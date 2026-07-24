using Microsoft.Extensions.DependencyInjection;
namespace TmsApi.Infrastructure.Services;
using TmsApi.Application.Interfaces;
public class EnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrollmentWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void ProcessBatch()
    {
        using var scope = _scopeFactory.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IEnrollmentService>();

        // .Result ከተጠቀምክ በኋላ በቀጥታ ሊንክ .Count() በመጠቀም እንቆጥራለን
        var enrollments = service.GetAllAsync().Result;
        var count = enrollments.Count();

        Console.WriteLine($"Found {count} enrollments");
    }
}