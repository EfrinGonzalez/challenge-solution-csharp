using Challenge.src.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static Challenge.src.HarnessSimulator.ChallengeMain;

namespace Challenge.src.Domain
{
    public sealed class OrderState
    {    
        public required Order Order { get; init; }  // your DTO from Client.cs
        public required Target Target { get; set; }
        public required DateTime LastUpdateUtc { get; set; }
        public double BudgetSec { get; set; }   // remaining freshness (ideal-temp seconds)
        public int Rate { get; set; }           // 1 at ideal, 2 off-ideal
    }
}
