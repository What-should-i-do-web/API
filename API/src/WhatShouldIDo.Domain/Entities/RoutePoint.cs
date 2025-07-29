using WhatShouldIDo.Domain.Exceptions;
using WhatShouldIDo.Domain.ValueObjects;

namespace WhatShouldIDo.Domain.Entities
{
    public class RoutePoint : EntityBase
    {
        public Guid RouteId { get; set; }
        public Coordinates Location { get; private set; }
        public int Order { get; private set; }

        private RoutePoint() { } // EF Core için

        public RoutePoint(Guid routeId, Coordinates location, int order)
        {
            if (routeId == Guid.Empty)
                throw new DomainException("RouteId is required.");
            Location = location ?? throw new DomainException("Location is required.");
            if (order < 0)
                throw new DomainException("Order must be non-negative.");

            Id = Guid.NewGuid();
            RouteId = routeId;
            Order = order;
        }

        public void UpdateOrder(int newOrder)
        {
            if (newOrder < 0)
                throw new DomainException("Order must be non-negative.");
            Order = newOrder;
        }
    }
}
