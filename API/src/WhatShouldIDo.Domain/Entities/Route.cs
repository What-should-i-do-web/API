using WhatShouldIDo.Domain.Exceptions;

namespace WhatShouldIDo.Domain.Entities
{
    public class Route : EntityBase
    {
        public string Name { get; private set; }
        public DateTime CreatedAt { get; private set; }
        private readonly List<RoutePoint> _points = new();
        public IReadOnlyCollection<RoutePoint> Points => _points.AsReadOnly();

        private Route() { }

        public Route(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Route name is required.");

            Id = Guid.NewGuid();
            Name = name.Trim();
            CreatedAt = DateTime.UtcNow;
        }

        public void AddPoint(RoutePoint point)
        {
            if (point == null)
                throw new DomainException("RoutePoint cannot be null.");

            _points.Add(point);
        }

        public void UpdateName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new DomainException("Route name is required.");
            Name = newName.Trim();
        }
    }
}
