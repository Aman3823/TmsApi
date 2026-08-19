using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Dtos;
namespace TmsApi.Api.Controllers;
[ApiController]
[Route("api/{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login(
        [FromBody] LoginRequest request,
        [FromServices] IWebHostEnvironment env
    )
    {
        // Validate credentials (demo account for M10 transporttesting)
        if(request.Username == "admin"&& request.Password == "password123!")
        {
            var dummyJwt = "header.payload.signature-demo-token";
            // Append HttpOnly authentication cookie — JavaScriptCANNOT read this token
            Response.Cookies.Append("tms_auth",dummyJwt,new CookieOptions
            {
                HttpOnly =true,
                Secure =!env.IsDevelopment(),// Development ላይ በ HTTP እንዲሰራ ያስችላል
                SameSite= SameSiteMode.Strict,
                Expires =DateTimeOffset.UtcNow.AddHours(2)
            }
            );
            return Ok(new UserProfileDto("System Admin",
"Admin"));
        }
return Unauthorized(new {detail = "Invalid username orpassword." });

        
    }
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        // Inspect cookie attached automatically by the browseron cross-origin requests
        // ብራውዘሩ በራሱ ያያያዘውን HttpOnly Cookie ማረጋገጥ
        if (Request.Cookies.TryGetValue("tms_auth",out _))
        {
            return Ok(new UserProfileDto("System Admin", "Admin"));
        }
        return Unauthorized(new{detail ="Session expired ormissing authentication cookie."});

    }
}