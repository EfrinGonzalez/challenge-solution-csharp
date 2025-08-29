using Challenge.src.Infra;
using Challenge.src.Services;
using Challenge.src.Settings;

namespace Challenge.src.HarnessSimulator;
class ChallengeMain { 

    
    static async Task Main(
        string? auth = null
      )
    {
        AppSettings settings = ConfigLoader.Load();

        if (!string.IsNullOrWhiteSpace(auth))
            settings.Challenge.Auth = auth;

        string endpoint = settings.Challenge.Endpoint;
        string name = settings.Challenge.Name;
        long seed = settings.Challenge.Seed;
        int rate = settings.Harness.RateMs;
        int min = settings.Harness.MinPickupSec;
        int max = settings.Harness.MaxPickupSec;
        try
        {
            var client = new Client(endpoint, auth);

            var problem = await client.NewProblemAsync(name, seed);


            var kitchenManager = new KitchenManager(settings.Storage);
            var rng = seed != 0 ? new Random(unchecked((int)(seed ^ (seed >> 32)))) : new Random();
            var pickupTasks = new List<Task>();

            foreach (var order in problem.Orders)
            {
                Console.WriteLine($"Received: {order}");

                lock (kitchenManager.Sync) kitchenManager.PlaceOrder(order, DateTime.UtcNow);

                var delayMs = rng.Next(min * 1000, max * 1000 + 1);
                pickupTasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(delayMs);
                    lock (kitchenManager.Sync) kitchenManager.PickupOrder(order.Id, DateTime.UtcNow);
                }));

                await Task.Delay(rate); // placement rate
            }

            await Task.WhenAll(pickupTasks);

            var result = await client.SolveAsync(
                problem.TestId,
                TimeSpan.FromMilliseconds(rate),
                TimeSpan.FromSeconds(min),
                TimeSpan.FromSeconds(max),
                kitchenManager.Actions); // KitchenManager collected actions

            Console.WriteLine($"Result: {result}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Simulation failed: {e}");
        }
    }

}
