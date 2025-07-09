using WhatShouldIDo.Domain.Exceptions;
using WhatShouldIDo.Domain.ValueObjects;

namespace WhatShouldIDo.Domain.Entities
{
    public class RoutePoint : EntityBase
    {
        public Coordinates Location { get; private set; }
        public int Order { get; private set; }

        private RoutePoint() { }

        public RoutePoint(Coordinates location, int order)
        {
            Location = location ?? throw new DomainException("Location is required.");
            if (order < 0)
                throw new DomainException("Order must be non-negative.");

            Id = Guid.NewGuid();
            Order = order;
        }
    }
}
