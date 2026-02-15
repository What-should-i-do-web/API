namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// DTO representing a route
    /// </summary>
    public class RouteDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid UserId { get; set; }
        public double TotalDistance { get; set; }
        public int EstimatedDuration { get; set; }
        public int StopCount { get; set; }
        public string TransportationMode { get; set; } = "walking";
        public string? RouteType { get; set; }
        public List<string>? Tags { get; set; }
        public bool IsPublic { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<RoutePointDto>? Points { get; set; }
    }
}
