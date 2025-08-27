using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Domain.Entities
{
    public class Suggestion
    {
        public Guid Id { get; set; }
        public string UserHash { get; set; }
        public string PlaceName { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }

        public string Category { get; set; }
        public string Source { get; set; }

        public string Reason { get; set; }
        public double Score { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? PhotoReference { get; set; }      // Google photo reference
        public string? PhotoUrl { get; set; }            // Generated photo URL
    }
}
