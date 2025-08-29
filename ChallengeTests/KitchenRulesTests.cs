using System;
using System.Linq;
using NUnit.Framework;
using Challenge.src.Services;
using Challenge.src.Domain;

namespace Challenge.Tests;
/// <summary>
/// Kitchen rule (shelf-full -> move then place)
/// </summary>
public class KitchenRulesTests
{
    static Order O(string id, string temp, int fresh = 60) => new(id, $"Item-{id}", temp, 10, fresh);

    [Test]
    public void ShelfFull_MovesHotFromShelf_ThenPlacesNewOnShelfTest()
    {
        var km = new KitchenManager();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Fill heater so next hot lands on shelf:
        for (int i = 0; i < 6; i++) km.PlaceOrder(O($"H{i}", "hot"), t0.AddMilliseconds(i));
        km.PlaceOrder(O("H_SHELF", "hot"), t0.AddSeconds(1)); // goes to shelf
        km.PickupOrder("H0", t0.AddSeconds(2));              // free a heater slot

        // Fill remaining 11 shelf slots with room items:
        for (int i = 0; i < 11; i++) km.PlaceOrder(O($"R{i}", "room"), t0.AddSeconds(3 + i));

        // Now shelf full + heater has room → placing any order should move hot from shelf to heater
        km.PlaceOrder(O("NEW", "room"), t0.AddSeconds(20));

        Assert.Multiple(() =>
        {
            Assert.That(km.Actions.Any(a => a.Id == "H_SHELF" && a.Action == "move" && a.Target == "heater"), Is.True);
            Assert.That(km.Actions.Any(a => a.Id == "NEW" && a.Action == "place" && a.Target == "shelf"), Is.True);
        });
    }
}

