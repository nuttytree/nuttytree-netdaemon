using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuttyTree.NetDaemon.Infrastructure.Database;

namespace NuttyTree.NetDaemon.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AppointmentRemindersController : ControllerBase
{
    private readonly NuttyDbContext dbContext;

    public AppointmentRemindersController(NuttyDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointmentsAsync()
    {
        var appointments = await dbContext.AppointmentReminders
            .OrderBy(r => r.NextAnnouncement)
            .Select(r => new
            {
                r.Appointment.Id,
                r.Appointment.StartDateTime,
                r.Appointment.Summary,
                r.NextAnnouncement
            })
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(appointments);
    }
}
