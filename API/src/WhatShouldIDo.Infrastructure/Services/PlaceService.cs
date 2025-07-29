using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class PlaceService : IPlaceService
    {
        private readonly WhatShouldIDoDbContext _context;
        private readonly ILogger<PlaceService> _logger;

        public PlaceService(WhatShouldIDoDbContext context, ILogger<PlaceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> UpdateSponsorshipAsync(UpdatePlaceSponsorshipRequest request)
        {
            var place = await _context.Places.FindAsync(request.PlaceId);
            if (place == null)
                return false;

            place.IsSponsored = request.IsSponsored;
            place.SponsoredUntil = request.SponsoredUntil;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Place sponsorship updated for {id}", place.Id);

            return true;
        }
        private List<Place> CleanExpiredSponsorships(List<Place> places)
        {
            foreach (var place in places)
            {
                if (place.IsSponsored && place.SponsoredUntil.HasValue && place.SponsoredUntil < DateTime.UtcNow)
                {
                    place.IsSponsored = false;
                }
            }

            return places;
        }

    }

}
