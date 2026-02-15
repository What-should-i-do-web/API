namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// DTO representing a single point in a route
    /// </summary>
    public class RoutePointDto
    {
        public Guid Id { get; set; }
        public Guid RouteId { get; set; }
        public int Order { get; set; }
        public string PlaceId { get; set; } = string.Empty;
        public string PlaceName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int? EstimatedDuration { get; set; }
        public string? Notes { get; set; }
    }
}
