using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OlympiaStudent360.Api.Core.DTOs;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DashboardController : ControllerBase
{
    [HttpGet("summary")]
    public ActionResult<DashboardSummaryDto> Summary()
    {
        return Ok(new DashboardSummaryDto
        {
            Metrics =
            [
                new MetricCardDto { Label = "Students", Value = "-", Delta = "Awaiting data warehouse" },
                new MetricCardDto { Label = "Attendance", Value = "-", Delta = "Coming soon" },
                new MetricCardDto { Label = "Academic Alerts", Value = "-", Delta = "Coming soon" },
                new MetricCardDto { Label = "Engagement", Value = "-", Delta = "Coming soon" }
            ]
        });
    }
}
