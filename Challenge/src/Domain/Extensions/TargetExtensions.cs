using Challenge.src.Domain.Enum;

namespace Challenge.src.Domain.Extensions
{
    public static class TargetExtensions
    {
         public static Target IdealFor(string? temp) => TempExtensions.Normalize(temp) switch
        {
            "hot" => Target.Heater,
            "cold" => Target.Cooler,
            _ => Target.Shelf
        };

        public static int RateFor(Target loc, string? temp) => (loc, TempExtensions.Normalize(temp)) switch
        {
            (Target.Heater, "hot") => 1,
            (Target.Cooler, "cold") => 1,
            (Target.Shelf, "room") => 1,
            _ => 2
        };

         public static string TargetName(Target loc) => loc switch
        {
            Target.Heater => "heater",
            Target.Cooler => "cooler",
            _ => "shelf"
        };
    }
}
