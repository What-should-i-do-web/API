using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services;

public class Ranker
{
    public List<Place> Rank(List<Place> places, float userLat, float userLng)
    {
        return places
            .Select(place => new { Place = place, Score = CalculateScore(place, userLat, userLng) })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Place)
            .ToList();
    }
    
    private static double CalculateScore(Place place, float userLat, float userLng)
    {
        var distance = CalculateDistance(userLat, userLng, place.Latitude, place.Longitude);
        var proximityScore = Math.Max(0, 1 - (distance / 5000)) * 0.4;
        
        var ratingScore = double.TryParse(place.Rating, out var rating) ? (rating / 5.0) * 0.2 : 0;
        var reviewScore = 0.1;
        
        var sourceScore = place.Source == "Google" ? 0.25 : 0.15;
        var sponsorScore = place.IsSponsored ? 0.1 : 0.05;
        
        return proximityScore + ratingScore + reviewScore + sourceScore + sponsorScore;
    }
    
    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
    
    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}