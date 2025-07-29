using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IPlaceService _placeService;

        public AdminController(IPlaceService placeService)
        {
            _placeService = placeService;
        }

        [HttpPut("place/sponsor")]
        public async Task<IActionResult> UpdateSponsorship([FromBody] UpdatePlaceSponsorshipRequest request)
        {
            var result = await _placeService.UpdateSponsorshipAsync(request);
            if (!result)
                return NotFound(new { message = "Place not found." });

            return Ok(new { message = "Sponsorship updated successfully." });
        }
    }

}
