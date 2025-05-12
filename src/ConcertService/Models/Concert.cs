using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace ConcertService.Models
{
    public class Concert
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } // This will be the ConcertID

        [BsonElement("Name")]
        public required string Name { get; set; }

        [BsonElement("Venue")]
        public required string Venue { get; set; }

        [BsonElement("Date")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Date { get; set; }

        [BsonElement("StartTime")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] // Store as DateTime, can extract time part if needed
        public DateTime StartTime { get; set; }

        [BsonElement("Description")]
        public string? Description { get; set; }

        [BsonElement("SeatTypes")]
        public List<SeatType> SeatTypes { get; set; } = new List<SeatType>();

        [BsonElement("IsBookingEnabled")] // For bonus requirement: Automatically disable bookings
        public bool IsBookingEnabled { get; set; } = true;

        [BsonElement("CreatedAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}