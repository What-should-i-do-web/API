using System;
using System.Text.Json;

namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// Taste profile event for history display.
    /// </summary>
    public class TasteEventDto
    {
        public Guid EventId { get; set; }
        public Guid UserId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public JsonElement? Payload { get; set; }
    }
}
