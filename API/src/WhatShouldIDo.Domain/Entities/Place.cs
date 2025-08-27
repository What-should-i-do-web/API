using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Domain.Entities
{
    public class Place
    {
        public Guid Id { get; set; }                     
        public string Name { get; set; }                 
        public float Latitude { get; set; }               
        public float Longitude { get; set; }              
        public string Address { get; set; }              
        public string Rating { get; set; }                // From Google
        public string Category { get; set; }              // Mapped from Google types
        public string GooglePlaceId { get; set; }         // To uniquely reference
        public string GoogleMapsUrl { get; set; }         // Link to view
        public DateTime CachedAt { get; set; }   // For cache TTL
        public string ?Source { get; set; }  // e.g., "PlacesAPI"
        public string? PriceLevel { get; set; }
        public bool IsSponsored { get; set; } = false;
        public DateTime? SponsoredUntil { get; set; }
        public string? PhotoReference { get; set; }      // Google photo reference
        public string? PhotoUrl { get; set; }            // Generated photo URL

    }

}
