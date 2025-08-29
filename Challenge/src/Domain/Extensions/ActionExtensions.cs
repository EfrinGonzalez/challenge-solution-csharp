namespace Challenge.src.Domain.Extensions
{
    public static class ActionExtensions
    {
        public static string ActionName(Enum.Action k) => k switch
        {
            Enum.Action.Place => "place",
            Enum.Action.Move => "move",
            Enum.Action.Pickup => "pickup",
            _ => "discard"
        };
    }
}
