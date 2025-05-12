using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace ConcertService.Models
{
    public class SeatType
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("Name")]
        public required string Name { get; set; }

        [BsonElement("Price")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Price { get; set; }

        [BsonElement("TotalSeats")]
        public int TotalSeats { get; set; }


        [BsonElement("RemainingSeats")]
        public int RemainingSeats { get; set; }
    }
}