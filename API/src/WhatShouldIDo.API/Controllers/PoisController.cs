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
    }
}
