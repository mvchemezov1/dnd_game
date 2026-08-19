using System.Text.Json;
using StackExchange.Redis;

namespace dnd_game.Infrastructure.Caching
{
    public class RedisCacheProvider : ICacheProvider
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisCacheProvider(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = redis.GetDatabase();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            if (expiry.HasValue)
                await _db.StringSetAsync(key, json, expiry.Value);
            else
                await _db.StringSetAsync(key, json);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            await _db.KeyDeleteAsync(key);
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _db.KeyExistsAsync(key);
        }
    }
}