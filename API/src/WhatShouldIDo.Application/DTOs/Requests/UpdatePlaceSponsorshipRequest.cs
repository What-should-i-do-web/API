using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    public class UpdatePlaceSponsorshipRequest
    {
        public Guid PlaceId { get; set; }
        public bool IsSponsored { get; set; }
        public DateTime? SponsoredUntil { get; set; }
    }
}
