using Challenge.src.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
