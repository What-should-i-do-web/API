using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for getting directions and travel information between locations
    /// </summary>
    public interface IDirectionsService
    {
        /// <summary>
        /// Gets directions from origin to destination
        /// </summary>
        Task<DirectionsResult> GetDirectionsAsync(
            double originLat,
            double originLng,
            double destLat,
            double destLng,
            string mode = "driving",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets distance matrix for multiple origins and destinations
        /// </summary>
        Task<DistanceMatrix> GetDistanceMatrixAsync(
            List<(double lat, double lng)> origins,
            List<(double lat, double lng)> destinations,
            string mode = "driving",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Estimates travel time and distance between two points
        /// </summary>
        Task<TravelEstimate> EstimateTravelAsync(
            double originLat,
            double originLng,
            double destLat,
            double destLng,
            string mode = "driving",
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result from directions API
    /// </summary>
    public class DirectionsResult
    {
        public int DistanceMeters { get; set; }
        public int DurationSeconds { get; set; }
        public string DurationText { get; set; } = string.Empty;
        public string DistanceText { get; set; } = string.Empty;
        public List<DirectionsStep> Steps { get; set; } = new();
        public string? PolylineEncoded { get; set; }
    }

    /// <summary>
    /// A step in the directions
    /// </summary>
    public class DirectionsStep
    {
        public int DistanceMeters { get; set; }
        public int DurationSeconds { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double EndLat { get; set; }
        public double EndLng { get; set; }
    }

    /// <summary>
    /// Distance matrix containing travel times/distances between multiple points
    /// </summary>
    public class DistanceMatrix
    {
        public int OriginCount { get; set; }
        public int DestinationCount { get; set; }
        public List<DistanceMatrixRow> Rows { get; set; } = new();
    }

    /// <summary>
    /// A row in the distance matrix
    /// </summary>
    public class DistanceMatrixRow
    {
        public List<DistanceMatrixElement> Elements { get; set; } = new();
    }

    /// <summary>
    /// An element in the distance matrix (origin -> destination)
    /// </summary>
    public class DistanceMatrixElement
    {
        public int DistanceMeters { get; set; }
        public int DurationSeconds { get; set; }
        public string Status { get; set; } = "OK";
    }

    /// <summary>
    /// Simple travel estimate
    /// </summary>
    public class TravelEstimate
    {
        public int DistanceMeters { get; set; }
        public int DurationSeconds { get; set; }
        public string Mode { get; set; } = string.Empty;
    }
}
