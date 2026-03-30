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
    public class WebsitesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly MqttPublishService _mqttPublishService;

        public WebsitesController(AppDbContext context, MqttPublishService mqttPublishService)
        {
            _context = context;
            _mqttPublishService = mqttPublishService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var websites = await _context.Websites
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
            var website = await _context.Websites
                .Select(w => new
                {
                    id = w.Id,
                    url = w.Url,
                    intervalTime = w.IntervalTime,
                    userId = w.UserId
                })
                .FirstOrDefaultAsync(w => w.id == id);

            if (website == null)
            {
                return NotFound("Website blev ikke fundet.");
            }

            return Ok(website);
        }

        [HttpGet("{id:long}/status")]
        public async Task<IActionResult> GetStatus(long id)
        {
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

        [HttpGet("{id:long}/ping")]
        public async Task<IActionResult> GetPing(long id)
        {
            var websiteExists = await _context.Websites.AnyAsync(w => w.Id == id);

            if (!websiteExists)
            {
                return NotFound("Website blev ikke fundet");     
            }

            var latestMeasurement = await _context.Measurements
                .Where(m => m.WebsiteId == id)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    statusCode = m.StatusCode,
                    dnsLookupMs = m.DnsLookupMs,
                    connectMs = m.ConnectMs,
                    tlsHandshakeMs = m.TlsHandshakeMs,
                    timeToFirstByteMs = m.TimeToFirstByteMs,
                    totalTimeMs = m.TotalTimeMs
                })
                .FirstOrDefaultAsync();

            if (latestMeasurement == null)
            {
                return NotFound("Ingen målinger på denne website");
            }

            return Ok(latestMeasurement);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWebsiteDto dto)
        {
            var accountExists = await _context.Users.AnyAsync(u => u.Id == dto.UserId);

            if (!accountExists)
            {
                return BadRequest("Account findes ikke.");
            }

            var normalizedUrl = dto.Url.Trim().ToLower();

            var websiteAlreadyExists = await _context.Websites
                .AnyAsync(w => w.UserId == dto.UserId && w.Url.ToLower() == normalizedUrl);

            if (websiteAlreadyExists)
            {
                return BadRequest("Du har allerede tilføjet dette website.");
            }

            var website = new Website
            {
                Url = dto.Url.Trim(),
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
            var website = await _context.Websites.FirstOrDefaultAsync(w => w.Id == id);

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