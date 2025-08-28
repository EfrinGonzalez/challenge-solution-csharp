// Challenge.Tests/FreshnessTests.cs
using System;
using NUnit.Framework;
using Challenge.src.Domain;
using Challenge.src.Domain.Enum;
using Challenge.src.Domain.Extensions;
using System.Security.Cryptography.X509Certificates;

namespace Challenge.Tests;

/// <summary>
/// Freshness/decay (pure extension)
/// </summary>
public class FreshnessTests
{
    [Test]
    public void AccumulateDecayTest()
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var s = new OrderState
        {
            Order = new Order("id", "Food", "hot", 10, 10),
            Target = Target.Heater,
            LastUpdateUtc = start,
            BudgetSec = 5, // only 5 seconds of freshness left
            Rate = 2       // decays at 2x speed (off-ideal)
        };

        s.AccumulateDecay(start.AddSeconds(3));

        Assert.That(s.BudgetSec, Is.EqualTo(0)); // 5 - 3*2 = -1 → clamp (force a value within a bound. It cannot be below cero.)
        Assert.That(s.LastUpdateUtc, Is.EqualTo(start.AddSeconds(3)));
    }
}
