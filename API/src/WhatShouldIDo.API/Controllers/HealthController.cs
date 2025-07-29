using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Infrastructure.Services;

namespace WhatShouldIDo.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly RedisHealthChecker _checker;

        public HealthController(RedisHealthChecker checker)
        {
            _checker = checker;
        }

        [HttpGet("redis")]
        public async Task<IActionResult> CheckRedis()
        {
            var ok = await _checker.TestAsync();
            return ok ? Ok("Redis bağlantısı başarılı") : StatusCode(500, "Redis bağlantısı başarısız");
        }
    }

}
