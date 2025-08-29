using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challenge.src.Settings
{
    public sealed class AppSettings
    {
        public ChallengeSettings Challenge { get; init; } = new();
        public HarnessSettings Harness { get; init; } = new();
        public StorageSettings Storage { get; init; } = new();
    }

    public sealed class ChallengeSettings
    {
        public string Endpoint { get; set; } = "https://api.cloudkitchens.com";
        public string Auth { get; set; } = "";
        public string Name { get; set; } = "";
        public long Seed { get; set; } = 0; // 0 = random on server
    }

    public sealed class HarnessSettings
    {
        public int RateMs { get; set; } = 500;
        public int MinPickupSec { get; set; } = 4;
        public int MaxPickupSec { get; set; } = 8;
    }

    public sealed class StorageSettings
    {
        public int HeaterCapacity { get; set; } = 6;
        public int CoolerCapacity { get; set; } = 6;
        public int ShelfCapacity { get; set; } = 12;
    }
}
