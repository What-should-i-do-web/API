using WhatShouldIDo.Domain.Exceptions;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Represents a historical snapshot of a route for versioning.
    /// Stores route data as JSONB for efficient storage.
    /// </summary>
    public class RouteRevision : EntityBase
    {
        /// <summary>
        /// The route this revision belongs to
        /// </summary>
        public Guid RouteId { get; private set; }

        /// <summary>
        /// Revision number (1, 2, 3, etc.)
        /// </summary>
        public int RevisionNumber { get; private set; }

        /// <summary>
        /// Serialized route data as JSON
        /// </summary>
        public string RouteDataJson { get; private set; }

        /// <summary>
        /// When this revision was created
        /// </summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Source of this revision (manual_edit, reroll, ai_generated, etc.)
        /// </summary>
        public string Source { get; private set; }

        /// <summary>
        /// Optional description of what changed
        /// </summary>
        public string? ChangeDescription { get; private set; }

        /// <summary>
        /// User who created this revision
        /// </summary>
        public Guid CreatedByUserId { get; private set; }

        /// <summary>
        /// Navigation property to the route
        /// </summary>
        public Route? Route { get; private set; }

        private RouteRevision() { }

        public RouteRevision(
            Guid routeId,
            int revisionNumber,
            string routeDataJson,
            Guid createdByUserId,
            string source,
            string? changeDescription = null)
        {
            if (routeId == Guid.Empty)
                throw new DomainException("RouteId is required.");
            if (revisionNumber < 1)
                throw new DomainException("RevisionNumber must be at least 1.");
            if (string.IsNullOrWhiteSpace(routeDataJson))
                throw new DomainException("RouteDataJson is required.");
            if (createdByUserId == Guid.Empty)
                throw new DomainException("CreatedByUserId is required.");
            if (string.IsNullOrWhiteSpace(source))
                throw new DomainException("Source is required.");

            Id = Guid.NewGuid();
            RouteId = routeId;
            RevisionNumber = revisionNumber;
            RouteDataJson = routeDataJson;
            CreatedByUserId = createdByUserId;
            Source = source;
            ChangeDescription = changeDescription;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the change description
        /// </summary>
        public void SetChangeDescription(string? description)
        {
            ChangeDescription = description;
        }
    }
}
