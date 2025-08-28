using Challenge.src.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challenge.src.Domain.Extensions
{
    public static class StorageLocationExtensions
    {
         public static StorageLocation IdealFor(string? temp) => TempExtensions.Normalize(temp) switch
        {
            "hot" => StorageLocation.Heater,
            "cold" => StorageLocation.Cooler,
            _ => StorageLocation.Shelf
        };

        public static int RateFor(StorageLocation loc, string? temp) => (loc, TempExtensions.Normalize(temp)) switch
        {
            (StorageLocation.Heater, "hot") => 1,
            (StorageLocation.Cooler, "cold") => 1,
            (StorageLocation.Shelf, "room") => 1,
            _ => 2
        };

         public static string TargetName(StorageLocation loc) => loc switch
        {
            StorageLocation.Heater => "heater",
            StorageLocation.Cooler => "cooler",
            _ => "shelf"
        };
    }
}
