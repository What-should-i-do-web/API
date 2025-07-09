using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoutesController : ControllerBase
    {
        private readonly IRouteService _routeService;

        public RoutesController(IRouteService routeService)
        {
            _routeService = routeService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RouteDto>>> GetAll()
        {
            var routes = await _routeService.GetAllAsync();
            return Ok(routes);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RouteDto>> GetById(Guid id)
        {
            var route = (await _routeService.GetAllAsync()).FirstOrDefault(r => r.Id == id);
            if (route == null) return NotFound();
            return Ok(route);
        }

        [HttpPost]
        public async Task<ActionResult<RouteDto>> Create([FromBody] CreateRouteRequest request)
        {
            var created = await _routeService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RouteDto>> Update(Guid id, [FromBody] UpdateRouteRequest request)
        {
            var updated = await _routeService.UpdateAsync(id, request);
            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _routeService.DeleteAsync(id);
            return NoContent();
        }
    }
}
