using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challenge.src.Domain
{
    /// <summary>
    /// Order is a json-friendly representation of an order.
    /// </summary>
    /// <param name="Id">order id</param>
    /// <param name="Name">food name</param>
    /// <param name="Temp">ideal temperature</param>
    /// <param name="Price">price in dollars</param>
    /// <param name="Freshness">freshness in seconds</param>
    public record Order(string Id, string Name, string Temp, long Price, long Freshness);
}
