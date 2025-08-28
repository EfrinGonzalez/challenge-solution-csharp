using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challenge.src.Domain.Extensions
{
    public static class TempExtensions
    {
        public static string Normalize(string? temp)
            => string.IsNullOrWhiteSpace(temp) ? "room" : temp.Trim().ToLowerInvariant();
    }
}
