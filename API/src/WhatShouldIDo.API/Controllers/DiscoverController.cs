using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscoverController : ControllerBase
    {
        private readonly ISuggestionService _suggestionService;

        public DiscoverController(ISuggestionService suggestionService)
        {
            _suggestionService = suggestionService;
        }

        // GET /api/discover?lat=...&lng=...&radius=...
        [HttpGet]
        public async Task<IActionResult> Discover([FromQuery] float lat, [FromQuery] float lng, [FromQuery] int radius = 3000)
        {
            var result = await _suggestionService.GetNearbySuggestionsAsync(lat, lng, radius);
            return Ok(result);
        }

        // GET /api/discover/random?lat=...&lng=...&radius=...
        [HttpGet("random")]
        public async Task<IActionResult> Random([FromQuery] float lat, [FromQuery] float lng, [FromQuery] int radius = 3000)
        {
            var result = await _suggestionService.GetRandomSuggestionAsync(lat, lng, radius);
            if (result == null)
                return NotFound("Uygun mekan bulunamadı.");
            return Ok(result);
        }

        // POST /api/discover/prompt
        [HttpPost("prompt")]
        public async Task<IActionResult> Prompt([FromBody] PromptRequest request)
        {
            var result = await _suggestionService.GetPromptSuggestionsAsync(request);
            return Ok(result);
        }
    }
}
