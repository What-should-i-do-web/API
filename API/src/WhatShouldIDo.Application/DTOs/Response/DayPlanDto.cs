namespace WhatShouldIDo.Application.DTOs.Response
{
    public class DayPlanDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime Date { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public string Budget { get; set; } = "";
        public List<PlannedActivityDto> Activities { get; set; } = new();
        public float TotalDistance { get; set; }
        public string Transportation { get; set; } = "";
        public bool IsPersonalized { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PlannedActivityDto
    {
        public int Order { get; set; }
        public string ActivityType { get; set; } = ""; // "historical", "restaurant", "entertainment", "break"
        public string PlaceName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string Reason { get; set; } = "";
        public TimeSpan StartTime { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public double Score { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Address { get; set; }
        public double? Rating { get; set; }
        public string? PriceLevel { get; set; }
    }
}