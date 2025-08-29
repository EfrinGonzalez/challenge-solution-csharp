namespace Challenge.src.Domain.Extensions
{
    public static class TempExtensions
    {
        public static string Normalize(string? temp)
            => string.IsNullOrWhiteSpace(temp) ? "room" : temp.Trim().ToLowerInvariant();
    }
}
