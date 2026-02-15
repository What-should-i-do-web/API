using WhatShouldIDo.Domain.Exceptions;

namespace WhatShouldIDo.Domain.Entities
{
    public class Route : EntityBase
    {
        public string Name { get; private set; }
        public string? Description { get; private set; }
        public Guid UserId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public bool IsPublic { get; private set; }
        public string? Tags { get; private set; }
        public double TotalDistance { get; private set; }
        public int EstimatedDuration { get; private set; }

        private readonly List<RoutePoint> _points = new();
        public IReadOnlyCollection<RoutePoint> Points => _points.AsReadOnly();

        private Route() { }

        public Route(string name, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Route name is required.");
            if (userId == Guid.Empty)
                throw new DomainException("UserId is required.");

            Id = Guid.NewGuid();
            Name = name.Trim();
            UserId = userId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            IsPublic = false;
            TotalDistance = 0;
            EstimatedDuration = 0;
        }

        public void AddPoint(RoutePoint point)
        {
            if (point == null)
                throw new DomainException("RoutePoint cannot be null.");

            _points.Add(point);
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new DomainException("Route name is required.");
            Name = newName.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateDescription(string? description)
        {
            Description = description?.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateDistanceAndDuration(double totalDistance, int estimatedDuration)
        {
            if (totalDistance < 0)
                throw new DomainException("Total distance must be non-negative.");
            if (estimatedDuration < 0)
                throw new DomainException("Estimated duration must be non-negative.");

            TotalDistance = totalDistance;
            EstimatedDuration = estimatedDuration;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetTags(string? tags)
        {
            Tags = tags;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetPublic(bool isPublic)
        {
            IsPublic = isPublic;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
