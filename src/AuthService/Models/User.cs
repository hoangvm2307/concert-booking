using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthService.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("Username")]
        public required string Username { get; set; }

        [BsonElement("Email")]
        public required string Email { get; set; }

        [BsonElement("PasswordHash")]
        public required string PasswordHash { get; set; }

        [BsonElement("RegisteredAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    }
}