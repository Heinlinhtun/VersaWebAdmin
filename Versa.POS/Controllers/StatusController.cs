using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Check()
    {
        return Ok(new
        {
            Status = "Online",
            Time = DateTime.Now,
            Message = "Versa POS API is running smoothly!"
        });
    }
}