using Challenge.src.Domain;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Actions = Challenge.src.Domain.Actions;

namespace Challenge.src.Infra;
/// <summary>
/// Client is a client for fetching and solving challenge test problems
/// </summary>
class Client(string endpoint, string auth) {

    private readonly string endpoint = endpoint, auth = auth;
    private readonly HttpClient client = new();
    
    /// <summary>
    ///  NewProblemAsync fetches a new test problem from the server. The URL also works in a browser for convenience.
    /// </summary>
    public async Task<Problem> NewProblemAsync(string name, long seed = 0) {
        if (seed == 0) {
            seed = new Random().NextInt64();
        }

        var url = $"{endpoint}/interview/challenge/new?auth={auth}&name={name}&seed={seed}";         
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode) {
            throw new Exception($"{url}: {response.StatusCode}");
        }

        var id = response.Headers.GetValues("x-test-id").First();
        Console.WriteLine($"Fetched new test problem, id={id}: {url}");

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        return new Problem(id, orders ?? []);
    }          

    /// <summary>
    /// SolveAsync submits a sequence of actions and parameters as a solution to a test problem. Returns test result.
    /// </summary>
    public async Task<string> SolveAsync(string testId, TimeSpan rate, TimeSpan min, TimeSpan max, List<Actions> actions) {    
        var solution = new Solution(new Options(rate, min, max), actions);

        var url = $"{endpoint}/interview/challenge/solve?auth={auth}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-test-id", testId);
        request.Content = new StringContent(JsonSerializer.Serialize(solution), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) {
            throw new Exception($"{url}: {response.StatusCode}");
        }

        return await response.Content.ReadAsStringAsync();
    }
}
