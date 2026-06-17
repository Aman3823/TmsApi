using System;
namespace TmsApi.Entities;
public class Enrollment
{
    public int Id{get;set;}
    public int StudentId{get;set;}
    public int CourseCode{get;set;}
    public decimal? Grade {get;set;}
    public DateTime EnrolledAt{get;set;} = DateTime.UtcNow;
    public Student Student{get;set;} = null!;
    public Course Course{get;set;} = null!;
}