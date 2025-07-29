using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoutePointsController : ControllerBase
    {
        private readonly IRoutePointService _routePointService;

        public RoutePointsController(IRoutePointService routePointService)
        {
            _routePointService = routePointService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoutePointDto>>> GetByRoute(Guid routeId)
            => Ok(await _routePointService.GetByRouteAsync(routeId));

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RoutePointDto>> GetById(Guid routeId, Guid id)
            => Ok(await _routePointService.GetByIdAsync(id));

        [HttpPost]
        public async Task<ActionResult<RoutePointDto>> Create(Guid routeId, [FromBody] CreateRoutePointRequest request)
        {
            // Ensure routeId matches
            request = request with { RouteId = routeId };
            var created = await _routePointService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { routeId = created.RouteId, id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RoutePointDto>> Update(Guid routeId, Guid id, [FromBody] UpdateRoutePointRequest request)
            => Ok(await _routePointService.UpdateAsync(id, request));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid routeId, Guid id)
        {
            await _routePointService.DeleteAsync(id);
            return NoContent();
        }
    }
}
