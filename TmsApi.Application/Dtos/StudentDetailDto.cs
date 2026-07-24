namespace TmsApi.Application.Dtos;

public class StudentDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public double GPA { get; set; }
    public bool IsActive { get; set; }
    public List<LinkDto> Links { get; set; } = new();
}