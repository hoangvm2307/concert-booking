using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BookingService.Models
{
    public enum BookingStatus
    {
        Pending,
        Confirmed,
        Cancelled,
        Failed
    }

    public class Booking
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public required string UserId { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public required string ConcertId { get; set; }

        public string? ConcertName { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public required string SeatTypeId { get; set; }

        public string? SeatTypeName { get; set; }
        public decimal PricePaid { get; set; }

        [BsonElement("BookingTime")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime BookingTime { get; set; } = DateTime.UtcNow;

        [BsonElement("Status")]
        [BsonRepresentation(BsonType.String)]
        public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    }
}