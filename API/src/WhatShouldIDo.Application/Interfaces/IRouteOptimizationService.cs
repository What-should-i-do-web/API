using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for optimizing routes using various algorithms (TSP, nearest neighbor, etc.)
    /// </summary>
    public interface IRouteOptimizationService
    {
        /// <summary>
        /// Optimizes the order of waypoints to minimize total travel distance/time
        /// Uses TSP solver for optimal route
        /// </summary>
        Task<OptimizedRoute> OptimizeRouteAsync(
            (double lat, double lng) startPoint,
            List<RouteWaypoint> waypoints,
            string transportMode = "driving",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reorders route points using nearest neighbor heuristic (faster, less optimal)
        /// </summary>
        Task<OptimizedRoute> OptimizeRouteNearestNeighborAsync(
            (double lat, double lng) startPoint,
            List<RouteWaypoint> waypoints,
            string transportMode = "driving",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates distance matrix for a set of points
        /// </summary>
        Task<double[,]> CalculateDistanceMatrixAsync(
            List<(double lat, double lng)> points,
            string transportMode = "driving",
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Waypoint in a route
    /// </summary>
    public class RouteWaypoint
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int? PreferredOrder { get; set; }
        public bool IsMandatory { get; set; } = true;
        public int? EstimatedDurationMinutes { get; set; }
    }

    /// <summary>
    /// Result of route optimization
    /// </summary>
    public class OptimizedRoute
    {
        public List<OptimizedWaypoint> OrderedWaypoints { get; set; } = new();
        public int TotalDistanceMeters { get; set; }
        public int TotalDurationSeconds { get; set; }
        public string OptimizationMethod { get; set; } = string.Empty;
        public double ImprovementPercentage { get; set; }
    }

    /// <summary>
    /// Waypoint with optimization metadata
    /// </summary>
    public class OptimizedWaypoint
    {
        public RouteWaypoint Waypoint { get; set; } = null!;
        public int OptimizedOrder { get; set; }
        public int DistanceFromPreviousMeters { get; set; }
        public int DurationFromPreviousSeconds { get; set; }
    }
}
