using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;
namespace TmsApi.Infrastructure.Persistence.Configurations;
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)

    
    {
        builder.HasKey(s =>s.Id);
        builder.Property(s =>s.Name).IsRequired().HasMaxLength(50);
        builder.Property(s => s.RegistrationNumber).IsRequired() .HasMaxLength(20);
        builder.Property(s =>s.GPA).HasColumnType("numeric(3,2)");
        builder.Property<DateTime>("LastUpdated")
        .HasDefaultValueSql("NOW()"); 
        builder.Property(s =>s.RowVersion)
        .IsRowVersion();
        
    }
}