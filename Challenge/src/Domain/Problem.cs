using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Challenge.src.Domain
{
    record Problem(string TestId, List<Order> Orders);
}
