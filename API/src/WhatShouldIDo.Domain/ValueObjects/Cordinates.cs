using WhatShouldIDo.Domain.Exceptions;

namespace WhatShouldIDo.Domain.ValueObjects
{
    public class Coordinates : IEquatable<Coordinates>
    {
        // EF Core için private setter’lar
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        // EF Core’un instance yaratabilmesi için parametresiz ctor
        private Coordinates() { }

        public Coordinates(double latitude, double longitude)
        {
            if (latitude < -90 || latitude > 90)
                throw new DomainException("Latitude must be between -90 and 90.");
            if (longitude < -180 || longitude > 180)
                throw new DomainException("Longitude must be between -180 and 180.");

            Latitude = latitude;
            Longitude = longitude;
        }

        public override bool Equals(object? obj) => Equals(obj as Coordinates);
        public bool Equals(Coordinates? other) =>
            other != null && Latitude == other.Latitude && Longitude == other.Longitude;
        public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);
    }
}
