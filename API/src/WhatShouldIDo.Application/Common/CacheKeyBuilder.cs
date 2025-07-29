using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.Common
{
    public static class CacheKeyBuilder
    {
        public static string Nearby(float lat, float lng, int radius, string keyword)
        {
            float roundedLat = (float)Math.Round(lat, 3);
            float roundedLng = (float)Math.Round(lng, 3);

            return $"cache-test-nearby:{roundedLat}:{roundedLng}:{radius}:{keyword?.ToLower() ?? "all"}";
        }

        public static string Prompt(string query, float lat, float lng)
        {
            float roundedLat = (float)Math.Round(lat, 3);
            float roundedLng = (float)Math.Round(lng, 3);

            return $"text:{query.ToLowerInvariant()}:{roundedLat}:{roundedLng}";
        }

        public static string Geocode(string locationText)
        {
            return $"geo:{locationText.ToLowerInvariant()}";
        }
    }
}

