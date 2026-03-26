using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UptimeDaddy.API.Data;
using UptimeDaddy.API.DTOs;
using UptimeDaddy.API.Models;
using UptimeDaddy.API.Services;

namespace UptimeDaddy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WebsitesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly MqttPublishService _mqttPublishService;

        public WebsitesController(AppDbContext context, MqttPublishService mqttPublishService)
        {
            _context = context;
            _mqttPublishService = mqttPublishService;
        }

        private bool TryGetCurrentUserId(out long userId)
        {
            userId = 0;

            var claimValue =
                User.FindFirst("sub")?.Value ??
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst("id")?.Value ??
                User.FindFirst("userId")?.Value;

            return long.TryParse(claimValue, out userId);
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized("Kunne ikke læse bruger-id fra token.");
            }

            var websites = await _context.Websites
                .Where(w => w.UserId == currentUserId)
                .Select(w => new
                {
                    id = w.Id,
                    url = w.Url,
                    intervalTime = w.IntervalTime,
                    userId = w.UserId
                })
                .ToListAsync();

            return Ok(websites);
        }

        [HttpGet("user/{userId:long}")]
        public async Task<IActionResult> GetByUser(long userId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized("Kunne ikke læse bruger-id fra token.");
            }

            if (currentUserId != userId)
            {
                return Forbid();
            }

            var websites = await _context.Websites
                .Where(w => w.UserId == userId)
                .Select(w => new
                {
                    id = w.Id,
                    url = w.Url,
                    intervalTime = w.IntervalTime,
                    userId = w.UserId
                })
                .ToListAsync();

            return Ok(websites);
        }

        [HttpGet("user/{userId:long}/with-measurements")]
        public async Task<IActionResult> GetByUserWithMeasurements(long userId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized("Kunne ikke læse bruger-id fra token.");
            }

            if (currentUserId != userId)
            {
                return Forbid();
            }

            var websites = await _context.Websites
                .Where(w => w.UserId == userId)
                .Include(w => w.Measurements)
                .Select(w => new
                {
                    id = w.Id,
                    url = w.Url,
                    intervalTime = w.IntervalTime,
                    userId = w.UserId,
                    measurements = w.Measurements
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => new
                        {
                            id = m.Id,
                            websiteId = m.WebsiteId,
                            statusCode = m.StatusCode,
                            dnsLookupMs = m.DnsLookupMs,
                            connectMs = m.ConnectMs,
                            tlsHandshakeMs = m.TlsHandshakeMs,
                            timeToFirstByteMs = m.TimeToFirstByteMs,
                            totalTimeMs = m.TotalTimeMs,
                            createdAt = m.CreatedAt
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(websites);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized("Kunne ikke læse bruger-id fra token.");
            }

            var website = await _context.Websites
                .Where(w => w.Id == id && w.UserId == currentUserId)
                .Select(w => new
                {
                    id = w.Id,
                    url = w.Url,
                    intervalTime = w.IntervalTime,
                    userId = w.UserId
                })
                .FirstOrDefaultAsync();

            if (website == null)
            {
                return NotFound("Website blev ikke fundet.");
            }

            return Ok(website);
        }

        [HttpGet("{id:long}/status")]
        public async Task<IActionResult> GetStatus(long id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized("Kunne ikke læse bruger-id fra token.");
            }

            var websiteExists = await _context.Websites
                .AnyAsync(w => w.Id == id && w.UserId == currentUserId);

            if (!websiteExists)
            {
                return NotFound("Website blev ikke fundet.");
            }

            var latestMeasurement = await _context.Measurements
                .Where(m => m.WebsiteId == id)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    websiteId = m.WebsiteId,
                    statusCode = m.StatusCode,
                    totalTimeMs = m.TotalTimeMs,
                    createdAt = m.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (latestMeasurement == null)
            {
                return NotFound("Ingen målinger fundet for dette website.");
            }

            return Ok(latestMeasurement);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWebsiteDto dto)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized("Kunne ikke læse bruger-id fra token.");
            }

            if (dto.UserId != currentUserId)
            {
                return Forbid();
            }

            var accountExists = await _context.Users.AnyAsync(u => u.Id == dto.UserId);

            if (!accountExists)
            {
                return BadRequest("Account findes ikke.");
            }

            var website = new Website
            {
                Url = dto.Url,
                IntervalTime = dto.IntervalTime,
                UserId = dto.UserId
            };

            _context.Websites.Add(website);
            await _context.SaveChangesAsync();

            await _mqttPublishService.PublishWebsiteCreatedAsync(
                website.UserId,
                website.Id,
                website.Url,
                website.IntervalTime
            );

            return Ok(new
            {
                id = website.Id,
                url = website.Url,
                intervalTime = website.IntervalTime,
                userId = website.UserId
            });
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized("Kunne ikke læse bruger-id fra token.");
            }

            var website = await _context.Websites
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == currentUserId);

            if (website == null)
            {
                return NotFound("Website blev ikke fundet.");
            }

            var userId = website.UserId;
            var websiteId = website.Id;

            _context.Websites.Remove(website);
            await _context.SaveChangesAsync();

            await _mqttPublishService.PublishWebsiteDeletedAsync(userId, websiteId);

            return NoContent();
        }
    }
}