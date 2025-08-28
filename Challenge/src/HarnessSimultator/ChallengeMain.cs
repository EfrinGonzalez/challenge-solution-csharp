using Challenge.src.Domain;
using Challenge.src.Domain.Enum;
using Challenge.src.Infra;
using Challenge.src.Services;
using Action = Challenge.src.Domain.Actions;

namespace Challenge.src.HarnessSimulator;

class ChallengeMain { 

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
    static async Task Main(
       string auth = "n7mmqne5zi1m",
       string endpoint = "https://api.cloudkitchens.com",
       string name = "",
       long seed = 0,
       int rate = 500,
       int min = 4,
       int max = 8)
    {
        try
        {
            var client = new Client(endpoint, auth);
            var problem = await client.NewProblemAsync(name, seed);

            var km = new KitchenManager();
            var rng = seed != 0 ? new Random(unchecked((int)(seed ^ (seed >> 32)))) : new Random();
            var pickupTasks = new List<Task>();

            foreach (var order in problem.Orders)
            {
                Console.WriteLine($"Received: {order}");

                lock (km.Sync) km.PlaceOrder(order, DateTime.UtcNow);

                var delayMs = rng.Next(min * 1000, max * 1000 + 1);
                pickupTasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(delayMs);
                    lock (km.Sync) km.PickupOrder(order.Id, DateTime.UtcNow);
                }));

                await Task.Delay(rate); // placement rate
            }

            await Task.WhenAll(pickupTasks);

            var result = await client.SolveAsync(
                problem.TestId,
                TimeSpan.FromMilliseconds(rate),
                TimeSpan.FromSeconds(min),
                TimeSpan.FromSeconds(max),
                km.Actions); // KitchenManager collected actions

            Console.WriteLine($"Result: {result}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Simulation failed: {e}");
        }
    }

}
