using Challenge.src.Domain;
using Challenge.src.Infra;
using Action = Challenge.src.Domain.Action;

namespace Challenge.src.HarnessSimulator;

class ChallengeMain {


    // Local modeling kept inside this file (no new files)
    enum StorageLocation { Heater, Cooler, Shelf }

    sealed class OrderState
    {
        public required Order Order { get; init; }  // your DTO from Client.cs
        public required StorageLocation StorageLocation { get; set; }
        public required DateTime LastUpdateUtc { get; set; }
        public double BudgetSec { get; set; }   // remaining freshness (ideal-temp seconds)
        public int Rate { get; set; }           // 1 at ideal, 2 off-ideal
    }


    /// <summary>
    /// This method returns the freshness‐decay multiplier based on where the order is stored and what its ideal temp is. 
    /// We compare the pair(storageLocation, temp) using a tuple pattern in a C# switch expression.
    /// We lowercase temp once(ToLowerInvariant()) so the match is case-insensitive.
    /// If the food is at its ideal location, return 1 (normal decay).
    /// Otherwise, return 2 (decays twice as fast), matching the spec.
    /// Using your current enum name in canvas (StorageLoc), the truth table is:

    /// StorageLoc   temp string	    return
    ///Heater	    "hot"	            1
    ///Cooler	    "cold"	            1
    ///Shelf	    "room"	            1
    ///anything else (off-ideal)	(e.g., hot on shelf, cold on heater, room in cooler, etc.)
    /// </summary>
    /// <param name="storageLocation"></param>
    /// <param name="temperature"></param>
    /// <returns>Anything else (off-ideal). (e.g., hot on shelf, cold on heater, room in cooler, etc.)</returns>
    static int RateFor(StorageLocation storageLocation, string temperature) => (storageLocation, temperature.ToLowerInvariant()) switch //Switch expression + tuple pattern
    {
        (StorageLocation.Heater, "hot") => 1,
        (StorageLocation.Cooler, "cold") => 1,
        (StorageLocation.Shelf, "room") => 1,
        _ => 2
    };

    static string TargetName(StorageLocation storageLocation) => storageLocation switch
    {
        StorageLocation.Heater => "heater",
        StorageLocation.Cooler => "cooler",
        _ => "shelf"
    };

    static StorageLocation IdealFor(string temp) => temp.ToLowerInvariant() switch
    {
        "hot" => StorageLocation.Heater,
        "cold" => StorageLocation.Cooler,
        _ => StorageLocation.Shelf
    };

    static void AccumulateDecay(OrderState s, DateTime nowUtc)
    {
        var elapsed = (nowUtc - s.LastUpdateUtc).TotalSeconds;
        if (elapsed > 0)
        {
            s.BudgetSec -= elapsed * s.Rate;
            s.LastUpdateUtc = nowUtc;
        }
    }


    /// <summary>
    /// Challenge harness
    /// </summary>
    /// <param name="auth">Authentication token (required)</param>
    /// <param name="endpoint">Problem server endpoint</param>
    /// <param name="name">Problem name. Leave blank (optional)</param>
    /// <param name="seed">Problem seed (random if zero)</param>
    /// <param name="rate">Inverse order rate (in milliseconds)</param>
    /// <param name="min">Minimum pickup time (in seconds)</param>
    /// <param name="max">Maximum pickup time (in seconds)</param>
    static async Task Main(string auth= "n7mmqne5zi1m", string endpoint = "https://api.cloudkitchens.com", string name = "", long seed = 0, int rate = 500, int min = 4, int max = 8) {
        //auth = "n7mmqne5zi1m";
        try {
            var client = new Client(endpoint, auth);
            var problem = await client.NewProblemAsync(name, seed);


            // ----- Kitchen state (thread-safe with a single lock) -----
            var sync = new object();

            const int HEATER_CAP = 6, COOLER_CAP = 6, SHELF_CAP = 12;

            var heater = new HashSet<string>();
            var cooler = new HashSet<string>();
            var shelf = new HashSet<string>();

            var states = new Dictionary<string, OrderState>();

            // Min-heap: (id, temp) by absolute shelf expiry ticks
            var shelfHeap = new PriorityQueue<(string id, string temp), long>();

            var actions = new List<Action>();
            var pickupTasks = new List<Task>();
            var rng = new Random();

            void Log(DateTime tsUtc, string id, string action, string target)
            {
                var a = new Action(tsUtc, id, action, target);
                actions.Add(a);
                Console.WriteLine($"{a.Timestamp} id={id} action={action} target={target}");
            }



            bool TryPlaceIdeal(OrderState s, DateTime nowUtc)
            {
                switch (s.StorageLocation)
                {
                    case StorageLocation.Heater:
                        if (heater.Count >= HEATER_CAP) return false;
                        heater.Add(s.Order.Id);
                        break;
                    case StorageLocation.Cooler:
                        if (cooler.Count >= COOLER_CAP) return false;
                        cooler.Add(s.Order.Id);
                        break;
                    case StorageLocation.Shelf:
                        if (shelf.Count >= SHELF_CAP) return false;
                        shelf.Add(s.Order.Id);
                        var shelfRate = RateFor(StorageLocation.Shelf, s.Order.Temp);
                        var expiryTicks = nowUtc.AddSeconds(s.BudgetSec / shelfRate).Ticks;
                        shelfHeap.Enqueue((s.Order.Id, s.Order.Temp), expiryTicks);
                        break;
                }
                s.Rate = RateFor(s.StorageLocation, s.Order.Temp);
                s.LastUpdateUtc = nowUtc;
                states[s.Order.Id] = s;
                Log(nowUtc, s.Order.Id, Action.Place, TargetName(s.StorageLocation));
                return true;
            }

            bool TryPlaceOnShelf(OrderState s, DateTime nowUtc)
            {
                if (shelf.Count >= SHELF_CAP) return false;
                shelf.Add(s.Order.Id);
                s.StorageLocation = StorageLocation.Shelf;
                s.Rate = RateFor(StorageLocation.Shelf, s.Order.Temp);
                s.LastUpdateUtc = nowUtc;
                states[s.Order.Id] = s;
                var shelfRate = s.Rate; // already correct
                var expiryTicks = nowUtc.AddSeconds(s.BudgetSec / shelfRate).Ticks;
                shelfHeap.Enqueue((s.Order.Id, s.Order.Temp), expiryTicks);
                Log(nowUtc, s.Order.Id, Action.Place, "shelf");
                return true;
            }

            // Pop earliest shelf item matching temp (hot/cold) and move to its ideal if there is room
            bool TryMoveFromShelfToIdeal(string temp, DateTime nowUtc)
            {
                // Only meaningful for hot/cold
                var t = temp.ToLowerInvariant();
                if (t != "hot" && t != "cold") return false;

                // Check destination capacity up front
                if (t == "hot" && heater.Count >= HEATER_CAP) return false;
                if (t == "cold" && cooler.Count >= COOLER_CAP) return false;

                while (shelfHeap.TryDequeue(out var item, out _))
                {
                    if (!shelf.Contains(item.id)) continue; // lazy deletion
                    if (!string.Equals(item.temp, temp, StringComparison.OrdinalIgnoreCase)) continue;

                    // Move candidate
                    if (!states.TryGetValue(item.id, out var s)) continue;

                    AccumulateDecay(s, nowUtc);
                    if (s.BudgetSec <= 0)
                    {
                        // expired while on shelf → discard
                        shelf.Remove(s.Order.Id);
                        states.Remove(s.Order.Id);
                        Log(nowUtc, s.Order.Id, Action.Discard, "shelf");
                        continue; // keep scanning
                    }

                    // Remove from shelf set
                    shelf.Remove(s.Order.Id);

                    // Place into ideal
                    if (t == "hot") { heater.Add(s.Order.Id); s.StorageLocation = StorageLocation.Heater; }
                    else { cooler.Add(s.Order.Id); s.StorageLocation = StorageLocation.Cooler; }

                    s.Rate = RateFor(s.StorageLocation, s.Order.Temp);
                    s.LastUpdateUtc = nowUtc;
                    Log(nowUtc, s.Order.Id, Action.Move, TargetName(s.StorageLocation));
                    return true;
                }
                return false;
            }

            bool TryMoveAnyFromShelfToIdeal(DateTime nowUtc)
            {
                bool heaterRoom = heater.Count < HEATER_CAP;
                bool coolerRoom = cooler.Count < COOLER_CAP;
                if (!heaterRoom && !coolerRoom) return false;

                // pop until we find the earliest-expiring hot/cold that has room in its ideal
                var buffer = new List<((string id, string temp) item, long prio)>();
                (string id, string temp) chosen = default;
                long chosenPrio = 0;
                bool found = false;

                while (shelfHeap.TryDequeue(out var item, out var prio))
                {
                    if (!shelf.Contains(item.id)) continue; // stale
                    var isHot = item.temp.Equals("hot", StringComparison.OrdinalIgnoreCase);
                    var isCold = item.temp.Equals("cold", StringComparison.OrdinalIgnoreCase);

                    if ((isHot && heaterRoom) || (isCold && coolerRoom)) { chosen = item; chosenPrio = prio; found = true; break; }

                    buffer.Add((item, prio)); // not eligible now, keep for requeue
                }
                // put back others
                foreach (var b in buffer) shelfHeap.Enqueue(b.item, b.prio);
                if (!found) return false;

                // move chosen
                if (!states.TryGetValue(chosen.id, out var s)) return false; // rare: vanished
                AccumulateDecay(s!, nowUtc);
                if (s!.BudgetSec <= 0) { shelf.Remove(s.Order.Id); states.Remove(s.Order.Id); Log(nowUtc, s.Order.Id, Action.Discard, "shelf"); return true; }

                shelf.Remove(s.Order.Id);
                if (chosen.temp.Equals("hot", StringComparison.OrdinalIgnoreCase)) { heater.Add(s.Order.Id); s.StorageLocation = StorageLocation.Heater; }
                else { cooler.Add(s.Order.Id); s.StorageLocation = StorageLocation.Cooler; }
                s.Rate = RateFor(s.StorageLocation, s.Order.Temp);
                s.LastUpdateUtc = nowUtc;
                Log(nowUtc, s.Order.Id, Action.Move, TargetName(s.StorageLocation));
                return true;
            }


            void DiscardEarliestFromShelf(DateTime nowUtc)
            {
                while (shelfHeap.TryDequeue(out var item, out _))
                {
                    if (!shelf.Contains(item.id)) continue; // stale
                    if (!states.TryGetValue(item.id, out var s)) { shelf.Remove(item.id); continue; }

                    // Update freshness before discarding (for correctness)
                    AccumulateDecay(s, nowUtc);

                    shelf.Remove(s.Order.Id);
                    states.Remove(s.Order.Id);
                    Log(nowUtc, s.Order.Id, Action.Discard, "shelf");
                    return;
                }
                // nothing to discard (shouldn't happen if we checked full)
            }

            void PlaceOrder(Order order, DateTime nowUtc)
            {
                // Build initial state (assume ideal first)
                var s = new OrderState
                {
                    Order = order,
                    StorageLocation = IdealFor(order.Temp),
                    LastUpdateUtc = nowUtc,
                    BudgetSec = order.Freshness, // seconds
                    Rate = 1
                };

                // 1) Try ideal
                if (TryPlaceIdeal(s, nowUtc)) return;

                // 2) Try shelf
                if (TryPlaceOnShelf(s, nowUtc)) return;

                // 3) Shelf full → try to move any hot/cold from shelf to its ideal (if heater/cooler has room)
                if (TryMoveAnyFromShelfToIdeal(nowUtc))
                {
                    // Now shelf has room for the new order
                    if (TryPlaceOnShelf(s, nowUtc)) return;
                }
                else
                {
                    // 4) Discard earliest shelf item to make room
                    DiscardEarliestFromShelf(nowUtc);
                    if (TryPlaceOnShelf(s, nowUtc)) return;
                }

                throw new InvalidOperationException("Placement failed even after making room.");
            }

            void PickupOrder(string id, DateTime nowUtc)
            {
                if (!states.TryGetValue(id, out var s)) return; // not present → no-op

                AccumulateDecay(s, nowUtc);
                if (s.BudgetSec <= 0)
                {
                    // discard expired
                    switch (s.StorageLocation)
                    {
                        case StorageLocation.Heater: heater.Remove(id); break;
                        case StorageLocation.Cooler: cooler.Remove(id); break;
                        case StorageLocation.Shelf: shelf.Remove(id); break; // heap lazy
                    }
                    states.Remove(id);
                    Log(nowUtc, id, Action.Discard, TargetName(s.StorageLocation));
                    return;
                }

                // pickup
                switch (s.StorageLocation)
                {
                    case StorageLocation.Heater: heater.Remove(id); break;
                    case StorageLocation.Cooler: cooler.Remove(id); break;
                    case StorageLocation.Shelf: shelf.Remove(id); break; // heap lazy
                }
                states.Remove(id);
                Log(nowUtc, id, Action.Pickup, TargetName(s.StorageLocation));
            }

            // ------ Simulation harness logic goes here using rate, min and max ----
            // ---- Place at rate; schedule pickups randomly in [min,max] ----

            //var actions = new List<Action>();
            foreach (var order in problem.Orders) {
                Console.WriteLine($"Received: {order}");

                //Check temp in the comming order and then fill the Action.*
                //if there is space, then we can add the order. Check documentation for this decision.

                //actions.Add(new Action(DateTime.Now, order.Id, Action.Place, Action.Cooler));
                //await Task.Delay(rate);

                // Place now
                lock (sync) PlaceOrder(order, DateTime.UtcNow);

                // Schedule pickup after random delay
                var delayMs = rng.Next(min * 1000, max * 1000 + 1);
                pickupTasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(delayMs);
                    lock (sync) PickupOrder(order.Id, DateTime.UtcNow);
                }));

                // Next placement at configured rate
                await Task.Delay(rate);
            }

            // ----------------------------------------------------------------------

            //var result = await client.SolveAsync(problem.TestId, TimeSpan.FromMilliseconds(rate), TimeSpan.FromSeconds(min), TimeSpan.FromSeconds(max), actions);
            //Console.WriteLine($"Result: {result}");

            // Wait for all pickups to finish before submitting
            await Task.WhenAll(pickupTasks);

            var result = await client.SolveAsync(
                problem.TestId,
                TimeSpan.FromMilliseconds(rate),
                TimeSpan.FromSeconds(min),
                TimeSpan.FromSeconds(max),
                actions);

            Console.WriteLine($"Result: {result}");

        } catch (Exception e) {
            Console.WriteLine($"Simulation failed: {e}");
        }
    }





}
