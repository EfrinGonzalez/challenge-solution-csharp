using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Challenge.src.Settings
{
    public static class ConfigLoader
    {
        public static AppSettings Load(string? path = null)
        {
            var settings = new AppSettings();
            var file = path ?? "appsettings.json";

            if (File.Exists(file))
            {
                using var s = File.OpenRead(file);
                var tmp = JsonSerializer.Deserialize<AppSettings>(s, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (tmp is not null) settings = tmp;
            }

            
            settings.Challenge.Endpoint = Get("CHALLENGE_ENDPOINT", settings.Challenge.Endpoint);           // Problem server endpoint.
            settings.Challenge.Auth = Get("CHALLENGE_AUTH", settings.Challenge.Auth);                       // Authentication token (required).
            settings.Challenge.Name = Get("CHALLENGE_NAME", settings.Challenge.Name);                       // Problem name. Leave blank (optional).
            settings.Challenge.Seed = GetLong("CHALLENGE_SEED", settings.Challenge.Seed);                   //Problem seed (random if zero).


            settings.Harness.RateMs = GetInt("HARNESS_RATE_MS", settings.Harness.RateMs);                   // Inverse order rate (in milliseconds).
            settings.Harness.MinPickupSec = GetInt("HARNESS_MIN_SEC", settings.Harness.MinPickupSec);       // Minimum pickup time (in seconds).
            settings.Harness.MaxPickupSec = GetInt("HARNESS_MAX_SEC", settings.Harness.MaxPickupSec);       // Maximum pickup time (in seconds).

            settings.Storage.HeaterCapacity = GetInt("STORAGE_HEATER_CAP", settings.Storage.HeaterCapacity); //Capacity in Heater. 
            settings.Storage.CoolerCapacity = GetInt("STORAGE_COOLER_CAP", settings.Storage.CoolerCapacity); //Capacity in Cooler.
            settings.Storage.ShelfCapacity = GetInt("STORAGE_SHELF_CAP", settings.Storage.ShelfCapacity);   //Capacity in Shelf.

            return settings;
        }

        private static string Get(string key, string def)
            => Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : def;

        private static int GetInt(string key, int def)
            => int.TryParse(Environment.GetEnvironmentVariable(key), out var n) ? n : def;
        private static long GetLong(string key, long def)
       => long.TryParse(Environment.GetEnvironmentVariable(key), out var n) ? n : def;
    }
}
