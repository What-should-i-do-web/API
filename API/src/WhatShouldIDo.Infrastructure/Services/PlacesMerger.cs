using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services;

public class PlacesMerger
{
    public List<Place> Merge(
        List<Place> googleResults, 
        List<Place> otmResults, 
        double dedupMeters)
    {
        var merged = new List<Place>(googleResults);
        
        foreach (var otmPlace in otmResults)
        {
            if (!HasNearbyDuplicate(merged, otmPlace, dedupMeters))
            {
                merged.Add(otmPlace);
            }
        }
        
        return merged;
    }
    
    private static bool HasNearbyDuplicate(List<Place> existing, Place candidate, double maxMeters)
    {
        return existing.Any(place => 
            CalculateDistance(place.Latitude, place.Longitude, candidate.Latitude, candidate.Longitude) <= maxMeters &&
            AreSimilarNames(place.Name, candidate.Name));
    }
    
    private static bool AreSimilarNames(string name1, string name2)
    {
        if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2)) return false;
        
        var clean1 = name1.ToLowerInvariant().Trim();
        var clean2 = name2.ToLowerInvariant().Trim();
        
        return clean1 == clean2 || 
               clean1.Contains(clean2) || 
               clean2.Contains(clean1) ||
               LevenshteinDistance(clean1, clean2) <= Math.Min(clean1.Length, clean2.Length) * 0.3;
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
    
    private static int LevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];
        
        for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;
        
        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }
        
        return matrix[s1.Length, s2.Length];
    }
}