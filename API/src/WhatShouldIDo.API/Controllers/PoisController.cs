using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PoisController : ControllerBase
    {
        private readonly IPoiService _poiService;

        public PoisController(IPoiService poiService)
        {
            _poiService = poiService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PoiDto>>> GetAll()
            => Ok(await _poiService.GetAllAsync());

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<PoiDto>> GetById(Guid id)
        {
            var poi = await _poiService.GetByIdAsync(id);
            if (poi == null) return NotFound();
            return Ok(poi);
        }

        [HttpPost]
        public async Task<ActionResult<PoiDto>> Create([FromBody] CreatePoiRequest request)
        {
            var created = await _poiService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<PoiDto>> Update(Guid id, [FromBody] UpdatePoiRequest request)
            => Ok(await _poiService.UpdateAsync(id, request));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _poiService.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Find nearby points of interest (historical places, landmarks, monuments, etc.)
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lng">Longitude</param>
        /// <param name="radius">Search radius in meters (default: 3000)</param>
        /// <param name="types">Comma-separated place types (e.g., historical_place,landmark,monument)</param>
        /// <param name="maxResults">Maximum number of results (default: 20)</param>
        /// <returns>List of nearby points of interest</returns>
        [HttpGet("nearby")]
        public async Task<ActionResult<IEnumerable<PoiDto>>> GetNearby(
            [FromQuery] float lat,
            [FromQuery] float lng,
            [FromQuery] int radius = 3000,
            [FromQuery] string? types = null,
            [FromQuery] int maxResults = 20)
        {
            var typeArray = string.IsNullOrEmpty(types) 
                ? new[] { "historical_place", "historical_landmark", "monument", "tourist_attraction", "museum" }
                : types.Split(',');

            var pois = await _poiService.GetNearbyAsync(lat, lng, radius, typeArray, maxResults);
            return Ok(pois);
        }
    }
}
