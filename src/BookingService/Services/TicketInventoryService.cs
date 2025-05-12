using StackExchange.Redis;

namespace BookingService.Services
{
    public class TicketInventoryService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<TicketInventoryService> _logger;

        private const string DecrementTicketsLuaScript = @"
            local current_tickets = redis.call('GET', KEYS[1])
            if current_tickets == false then
                return -1
            end
            if tonumber(current_tickets) > 0 then
                redis.call('DECR', KEYS[1])
                return 1
            else
                return 0
            end
        ";

        private const string IncrementTicketsLuaScript = @"
            if redis.call('EXISTS', KEYS[1]) == 1 then
                redis.call('INCR', KEYS[1])
                return 1
            else
                return -1 -- Key should exist if we are incrementing for a cancellation
            end
        ";


        public TicketInventoryService(IConnectionMultiplexer redis, ILogger<TicketInventoryService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        private string GetTicketKey(string concertId, string seatTypeId)
        {
            return $"tickets:{concertId}:{seatTypeId}";
        }

        public async Task InitializeTicketCountAsync(string concertId, string seatTypeId, int count)
        {
            var db = _redis.GetDatabase();
            var key = GetTicketKey(concertId, seatTypeId);
            await db.StringSetAsync(key, count);
            _logger.LogInformation("Initialized ticket count for {Key} to {Count}", key, count);
        }

        public async Task<DecrementResult> TryDecrementTicketCountAsync(string concertId, string seatTypeId)
        {
            var db = _redis.GetDatabase();
            var key = GetTicketKey(concertId, seatTypeId);

            try
            {
                var result = (long?)await db.ScriptEvaluateAsync(DecrementTicketsLuaScript, new RedisKey[] { key });

                return result switch
                {
                    1 => DecrementResult.Success,
                    0 => DecrementResult.SoldOut,
                    -1 => DecrementResult.KeyNotFound, // Or treat as SoldOut depending on desired behavior
                    _ => DecrementResult.Error // Should not happen with this script
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrementing ticket count for {Key}", key);
                return DecrementResult.Error;
            }
        }

        public async Task<IncrementResult> TryIncrementTicketCountAsync(string concertId, string seatTypeId)
        {
            var db = _redis.GetDatabase();
            var key = GetTicketKey(concertId, seatTypeId);

            try
            {
                var result = (long?)await db.ScriptEvaluateAsync(IncrementTicketsLuaScript, new RedisKey[] { key });
                return result switch
                {
                    1 => IncrementResult.Success,
                    -1 => IncrementResult.KeyNotFound,
                    _ => IncrementResult.Error
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing ticket count for {Key}", key);
                return IncrementResult.Error;
            }
        }


        public async Task<int?> GetRemainingTicketsAsync(string concertId, string seatTypeId)
        {
            var db = _redis.GetDatabase();
            var key = GetTicketKey(concertId, seatTypeId);
            var value = await db.StringGetAsync(key);
            if (value.TryParse(out int count))
            {
                return count;
            }
            _logger.LogWarning("Could not parse ticket count for {Key}. Value: {Value}", key, value);
            return null; // Or 0, or throw an exception, depending on how you want to handle missing keys
        }

        public async Task<bool> ClearInventoryForConcertAsync(string concertId)
        {
            var db = _redis.GetDatabase();
            // Get all servers (usually one for non-clustered Redis)
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keyPattern = $"tickets:{concertId}:*";
            long deletedCount = 0;

            _logger.LogInformation("Attempting to clear inventory keys matching pattern: {KeyPattern}", keyPattern);

            var keysToDelete = new List<RedisKey>();
            await foreach (var key in server.KeysAsync(pattern: keyPattern))
            {
                keysToDelete.Add(key);
            }

            if (keysToDelete.Any())
            {
                deletedCount = await db.KeyDeleteAsync(keysToDelete.ToArray());
                _logger.LogInformation("Deleted {DeletedCount} inventory keys for concert ID {ConcertId} matching pattern {KeyPattern}.", deletedCount, concertId, keyPattern);
            }
            else
            {
                _logger.LogInformation("No inventory keys found to delete for concert ID {ConcertId} matching pattern {KeyPattern}.", concertId, keyPattern);
            }
            // Return true if any keys were processed or if no keys were found (idempotent)
            return true;
        }
    }

    public enum DecrementResult
    {
        Success,
        SoldOut,
        KeyNotFound,
        Error
    }
    public enum IncrementResult
    {
        Success,
        KeyNotFound,
        Error
    }
}