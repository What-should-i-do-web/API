using WhatShouldIDo.Domain.Exceptions;
using WhatShouldIDo.Domain.ValueObjects;

namespace WhatShouldIDo.Domain.Entities
{
    public class Poi : EntityBase
    {
        public string Name { get; private set; }
        public Coordinates Location { get; private set; }
        public string? Description { get; private set; }

        private Poi() { }

        public Poi(string name, Coordinates location, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("POI name is required.");

            Id = Guid.NewGuid();
            Name = name.Trim();
            Location = location ?? throw new DomainException("Location is required.");
            Description = description;
        }

        public void UpdateDescription(string? description)
        {
            Description = description;
        }
    }
}
