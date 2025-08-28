using Challenge.src.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challenge.src.Domain.Extensions
{
    public static class KitchenActionExtensions
    {
        public static string ActionName(KitchenAction k) => k switch
        {
            KitchenAction.Place => "place",
            KitchenAction.Move => "move",
            KitchenAction.Pickup => "pickup",
            _ => "discard"
        };
    }
}
