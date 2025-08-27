using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;
using System.Security.Claims;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscoverController : ControllerBase
    {
        private readonly ISmartSuggestionService _smartSuggestionService;
        private readonly ISuggestionService _fallbackSuggestionService;

        public DiscoverController(ISmartSuggestionService smartSuggestionService, ISuggestionService fallbackSuggestionService)
        {
            _smartSuggestionService = smartSuggestionService;
            _fallbackSuggestionService = fallbackSuggestionService;
        }

        // GET /api/discover?lat=...&lng=...&radius=...
        [HttpGet]
        public async Task<IActionResult> Discover([FromQuery] float lat, [FromQuery] float lng, [FromQuery] int radius = 3000)
        {
            // Try personalized suggestions for authenticated users
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                try
                {
                    var personalizedResult = await _smartSuggestionService.GetPersonalizedNearbySuggestionsAsync(userId.Value, lat, lng, radius);
                    return Ok(new { personalized = true, suggestions = personalizedResult, userId = userId.Value });
                }
                catch
                {
                    // Fallback to basic suggestions on error
                }
            }
            
            // Fallback for non-authenticated users or errors
            var result = await _fallbackSuggestionService.GetNearbySuggestionsAsync(lat, lng, radius);
            return Ok(new { personalized = false, suggestions = result });
        }

        // GET /api/discover/random?lat=...&lng=...&radius=...
        [HttpGet("random")]
        public async Task<IActionResult> Random([FromQuery] float lat, [FromQuery] float lng, [FromQuery] int radius = 3000)
        {
            // Try personalized random for authenticated users
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                try
                {
                    var personalizedResult = await _smartSuggestionService.GetPersonalizedRandomSuggestionAsync(userId.Value, lat, lng, radius);
                    if (personalizedResult != null)
                        return Ok(new { personalized = true, suggestion = personalizedResult });
                }
                catch
                {
                    // Fallback on error
                }
            }
            
            // Fallback for non-authenticated users or errors
            var result = await _fallbackSuggestionService.GetRandomSuggestionAsync(lat, lng, radius);
            if (result == null)
                return NotFound("Uygun mekan bulunamadı.");
            return Ok(new { personalized = false, suggestion = result });
        }

        // POST /api/discover/prompt
        [HttpPost("prompt")]
        public async Task<IActionResult> Prompt([FromBody] PromptRequest request)
        {
            // Try personalized suggestions for authenticated users
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                try
                {
                    var personalizedResult = await _smartSuggestionService.GetPersonalizedSuggestionsAsync(userId.Value, request);
                    return Ok(new { personalized = true, suggestions = personalizedResult, userId = userId.Value });
                }
                catch
                {
                    // Fallback on error
                }
            }
            
            // Fallback for non-authenticated users or errors
            var result = await _fallbackSuggestionService.GetPromptSuggestionsAsync(request);
            return Ok(new { personalized = false, suggestions = result });
        }
        
        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             User.FindFirst("sub")?.Value;
            
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
