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
            var websites = await _context.Websites.ToListAsync();
            return Ok(websites);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var website = await _context.Websites.FirstOrDefaultAsync(w => w.Id == id);

            if (website == null)
            {
                return NotFound("Website blev ikke fundet.");
            }

            return Ok(website);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWebsiteDto dto)
        {
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

            return Ok(website);
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