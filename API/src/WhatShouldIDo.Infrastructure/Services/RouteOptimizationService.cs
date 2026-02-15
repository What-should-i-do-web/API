using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Service for optimizing routes using various algorithms
    /// Implements Nearest Neighbor and 2-Opt TSP heuristics
    /// </summary>
    public class RouteOptimizationService : IRouteOptimizationService
    {
        private readonly IDirectionsService _directionsService;
        private readonly ILogger<RouteOptimizationService> _logger;

        public RouteOptimizationService(
            IDirectionsService directionsService,
            ILogger<RouteOptimizationService> logger)
        {
            _directionsService = directionsService ?? throw new ArgumentNullException(nameof(directionsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OptimizedRoute> OptimizeRouteAsync(
            (double lat, double lng) startPoint,
            List<RouteWaypoint> waypoints,
            string transportMode = "driving",
            CancellationToken cancellationToken = default)
        {
            if (waypoints == null || !waypoints.Any())
            {
                return new OptimizedRoute
                {
                    OptimizationMethod = "None - Empty Route"
                };
            }

            _logger.LogInformation("Optimizing route with {Count} waypoints using TSP solver", waypoints.Count);

            try
            {
                // Step 1: Calculate distance matrix
                var points = new List<(double lat, double lng)> { startPoint };
                points.AddRange(waypoints.Select(w => (w.Latitude, w.Longitude)));

                var distanceMatrix = await CalculateDistanceMatrixAsync(points, transportMode, cancellationToken);

                // Step 2: Apply nearest neighbor heuristic to get initial tour
                var tour = NearestNeighborTSP(distanceMatrix, 0); // Start from index 0 (start point)

                // Step 3: Improve with 2-opt
                tour = TwoOptImprovement(tour, distanceMatrix);

                // Step 4: Build optimized route result
                var optimizedWaypoints = new List<OptimizedWaypoint>();
                int totalDistance = 0;
                int totalDuration = 0;

                for (int i = 1; i < tour.Count; i++) // Skip index 0 (start point)
                {
                    var waypointIndex = tour[i] - 1; // Adjust for start point
                    var waypoint = waypoints[waypointIndex];

                    var prevIndex = tour[i - 1];
                    var currIndex = tour[i];

                    var distanceMeters = (int)(distanceMatrix[prevIndex, currIndex] * 1000); // Convert km to meters
                    var durationSeconds = EstimateDuration(distanceMeters, transportMode);

                    optimizedWaypoints.Add(new OptimizedWaypoint
                    {
                        Waypoint = waypoint,
                        OptimizedOrder = i,
                        DistanceFromPreviousMeters = distanceMeters,
                        DurationFromPreviousSeconds = durationSeconds
                    });

                    totalDistance += distanceMeters;
                    totalDuration += durationSeconds;
                }

                var result = new OptimizedRoute
                {
                    OrderedWaypoints = optimizedWaypoints,
                    TotalDistanceMeters = totalDistance,
                    TotalDurationSeconds = totalDuration,
                    OptimizationMethod = "TSP: Nearest Neighbor + 2-Opt",
                    ImprovementPercentage = 0 // Would need original route to calculate
                };

                _logger.LogInformation("Route optimized: {Distance}m, {Duration}s", totalDistance, totalDuration);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize route");
                throw;
            }
        }

        public async Task<OptimizedRoute> OptimizeRouteNearestNeighborAsync(
            (double lat, double lng) startPoint,
            List<RouteWaypoint> waypoints,
            string transportMode = "driving",
            CancellationToken cancellationToken = default)
        {
            if (waypoints == null || !waypoints.Any())
            {
                return new OptimizedRoute
                {
                    OptimizationMethod = "None - Empty Route"
                };
            }

            _logger.LogInformation("Optimizing route with {Count} waypoints using Nearest Neighbor", waypoints.Count);

            try
            {
                // Calculate distance matrix
                var points = new List<(double lat, double lng)> { startPoint };
                points.AddRange(waypoints.Select(w => (w.Latitude, w.Longitude)));

                var distanceMatrix = await CalculateDistanceMatrixAsync(points, transportMode, cancellationToken);

                // Apply nearest neighbor
                var tour = NearestNeighborTSP(distanceMatrix, 0);

                // Build result
                var optimizedWaypoints = new List<OptimizedWaypoint>();
                int totalDistance = 0;
                int totalDuration = 0;

                for (int i = 1; i < tour.Count; i++)
                {
                    var waypointIndex = tour[i] - 1;
                    var waypoint = waypoints[waypointIndex];

                    var distanceMeters = (int)(distanceMatrix[tour[i - 1], tour[i]] * 1000);
                    var durationSeconds = EstimateDuration(distanceMeters, transportMode);

                    optimizedWaypoints.Add(new OptimizedWaypoint
                    {
                        Waypoint = waypoint,
                        OptimizedOrder = i,
                        DistanceFromPreviousMeters = distanceMeters,
                        DurationFromPreviousSeconds = durationSeconds
                    });

                    totalDistance += distanceMeters;
                    totalDuration += durationSeconds;
                }

                return new OptimizedRoute
                {
                    OrderedWaypoints = optimizedWaypoints,
                    TotalDistanceMeters = totalDistance,
                    TotalDurationSeconds = totalDuration,
                    OptimizationMethod = "Nearest Neighbor"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize route with nearest neighbor");
                throw;
            }
        }

        public async Task<double[,]> CalculateDistanceMatrixAsync(
            List<(double lat, double lng)> points,
            string transportMode = "driving",
            CancellationToken cancellationToken = default)
        {
            var n = points.Count;
            var matrix = new double[n, n];

            // Use Google Distance Matrix API for accurate distances
            try
            {
                var distanceMatrix = await _directionsService.GetDistanceMatrixAsync(
                    points,
                    points,
                    transportMode,
                    cancellationToken);

                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j)
                        {
                            matrix[i, j] = 0;
                        }
                        else
                        {
                            // Convert meters to kilometers
                            matrix[i, j] = distanceMatrix.Rows[i].Elements[j].DistanceMeters / 1000.0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get Google distance matrix, using Haversine fallback");

                // Fallback to Haversine distance
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        matrix[i, j] = i == j ? 0 : HaversineDistance(points[i], points[j]);
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Nearest Neighbor TSP heuristic
        /// </summary>
        private List<int> NearestNeighborTSP(double[,] distanceMatrix, int startIndex)
        {
            int n = distanceMatrix.GetLength(0);
            var tour = new List<int> { startIndex };
            var visited = new bool[n];
            visited[startIndex] = true;

            int current = startIndex;

            while (tour.Count < n)
            {
                double minDistance = double.MaxValue;
                int nearest = -1;

                for (int i = 0; i < n; i++)
                {
                    if (!visited[i] && distanceMatrix[current, i] < minDistance)
                    {
                        minDistance = distanceMatrix[current, i];
                        nearest = i;
                    }
                }

                if (nearest == -1) break; // No more unvisited nodes

                tour.Add(nearest);
                visited[nearest] = true;
                current = nearest;
            }

            return tour;
        }

        /// <summary>
        /// 2-Opt improvement algorithm
        /// </summary>
        private List<int> TwoOptImprovement(List<int> tour, double[,] distanceMatrix)
        {
            bool improved = true;
            var bestTour = new List<int>(tour);

            while (improved)
            {
                improved = false;

                for (int i = 1; i < bestTour.Count - 2; i++)
                {
                    for (int j = i + 1; j < bestTour.Count - 1; j++)
                    {
                        // Calculate current distance
                        double currentDistance =
                            distanceMatrix[bestTour[i - 1], bestTour[i]] +
                            distanceMatrix[bestTour[j], bestTour[j + 1]];

                        // Calculate new distance after swap
                        double newDistance =
                            distanceMatrix[bestTour[i - 1], bestTour[j]] +
                            distanceMatrix[bestTour[i], bestTour[j + 1]];

                        if (newDistance < currentDistance)
                        {
                            // Reverse the segment between i and j
                            bestTour.Reverse(i, j - i + 1);
                            improved = true;
                        }
                    }
                }
            }

            return bestTour;
        }

        /// <summary>
        /// Haversine distance calculation (fallback)
        /// </summary>
        private double HaversineDistance((double lat, double lng) point1, (double lat, double lng) point2)
        {
            const double R = 6371; // Earth radius in kilometers

            var lat1Rad = point1.lat * Math.PI / 180;
            var lat2Rad = point2.lat * Math.PI / 180;
            var deltaLat = (point2.lat - point1.lat) * Math.PI / 180;
            var deltaLng = (point2.lng - point1.lng) * Math.PI / 180;

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLng / 2) * Math.Sin(deltaLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        /// <summary>
        /// Estimate duration based on distance and transport mode
        /// </summary>
        private int EstimateDuration(int distanceMeters, string mode)
        {
            // Average speeds (m/s)
            var speed = mode.ToLower() switch
            {
                "walking" => 1.4,    // 5 km/h
                "bicycling" => 4.2,  // 15 km/h
                "driving" => 11.1,   // 40 km/h (urban)
                "transit" => 8.3,    // 30 km/h
                _ => 11.1
            };

            return (int)(distanceMeters / speed);
        }
    }
}
