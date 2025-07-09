using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.API.Models
{
    public class Route
    {
        public Guid Id { get; set; }
        public string ?Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICollection<RoutePoint> ?Points { get; set; }
    }
}
