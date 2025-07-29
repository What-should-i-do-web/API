using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Response
{
    public class SuggestionDto
    {
        public Guid Id { get; set; }
        public string PlaceName { get; set; }

        public float Latitude { get; set; }
        public float Longitude { get; set; }

        public string Category { get; set; }
        public string Source { get; set; }         // "Geoapify"

        public string Reason { get; set; }
        public double Score { get; set; }
        public DateTime CreatedAt { get; set; }

        public string UserHash { get; set; }       // (opsiyonel, Random kontrolü için)
        public bool IsSponsored { get; set; } = false;
        public DateTime? SponsoredUntil { get; set; }
    }

}
