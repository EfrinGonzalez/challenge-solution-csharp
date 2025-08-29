using Challenge.src.Domain;
using Challenge.src.Domain.Enum;
using Challenge.src.Domain.Extensions;
using Challenge.src.Settings;
using Actions = Challenge.src.Domain.Actions;

namespace Challenge.src.Services
{
    /// <summary>
    /// Encapsulates all placement/move/discard/pickup rules and state.
    /// Thread-safe via the exposed Sync lock.
    /// </summary>
    public sealed class KitchenManager(StorageSettings storage, Action<string>? log = null)
    {
        // ---- storage limits ----
        // capacities from settings
        private readonly int _heaterCap = storage.HeaterCapacity, _coolerCap = storage.CoolerCapacity, _shelfCap = storage.ShelfCapacity;
        //private const int _heaterCap = 6, _coolerCap = 6, _shelfCap = 12;

        // ---- synchronization & state ----
        public object Sync { get; } = new();

        private readonly HashSet<string> _heater = [];
        private readonly HashSet<string> _cooler = [];
        private readonly HashSet<string> _shelf = [];

        // Min-heap keyed by absolute shelf-expiry ticks
        private readonly PriorityQueue<(string id, string temp), long> _shelfHeap = new();

        private readonly Dictionary<string, OrderState> _states = [];

        private readonly List<Actions> _actions = [];
        public List<Actions> Actions => _actions;
        private readonly Action<string> _log = log ?? Console.WriteLine;

        // ---- public API ----
        public void PlaceOrder(Order order, DateTime nowUtc)
        {
            var orderState = new OrderState
            {
                Order = order,
                Target = TargetExtensions.IdealFor(order.Temp),
                LastUpdateUtc = nowUtc,
                BudgetSec = order.Freshness, // seconds
                Rate = 1
            };

            // 1) Try ideal location
            if (TryPlaceIdealLocation(orderState, nowUtc)) return;

            // 2) Try shelf
            if (TryPlaceOnShelf(orderState, nowUtc)) return;

            // 3) Shelf full -> try to move any hot/cold from shelf to its ideal (if heater/cooler has room)
            if (TryMoveAnyFromShelfToIdealLocation(nowUtc))
            {
                if (TryPlaceOnShelf(orderState, nowUtc)) return;
            }
            else
            {
                // 4) Discard earliest shelf item to make room
                DiscardEarliestFromShelf(nowUtc);
                if (TryPlaceOnShelf(orderState, nowUtc)) return;
            }

            throw new InvalidOperationException("Placement failed even after making room.");
        }

        public void PickupOrder(string id, DateTime nowUtc)
        {
            if (!_states.TryGetValue(id, out var orderState)) return; // not present -> do nothing.

            orderState.AccumulateDecay(nowUtc);
            if (orderState.BudgetSec <= 0)
            {
                // discard expired
                switch (orderState.Target)
                {
                    case Target.Heater: _heater.Remove(id); break;
                    case Target.Cooler: _cooler.Remove(id); break;
                    case Target.Shelf: _shelf.Remove(id); break; // heap is lazy = lazy deletion. We removed it from the shelf set now; we’ll clean up its heap node later when we pop.
                }
                _states.Remove(id);
                RecordAction(nowUtc, id, Domain.Enum.Action.Discard, orderState.Target);
                return;
            }

            // pickup
            switch (orderState.Target)
            {
                case Target.Heater: _heater.Remove(id); break;
                case Target.Cooler: _cooler.Remove(id); break;
                case Target.Shelf: _shelf.Remove(id); break; // heap is lazy = lazy deletion. We removed it from the shelf set now; we’ll clean up its heap node later when we pop.
            }
            _states.Remove(id);
            RecordAction(nowUtc, id, Domain.Enum.Action.Pickup, orderState.Target);
        }

        // ---- helpers (private) ----
        private bool TryPlaceIdealLocation(OrderState orderState, DateTime nowUtc)
        {
            switch (orderState.Target)
            {
                case Target.Heater:
                    if (_heater.Count >= _heaterCap) return false;
                    _heater.Add(orderState.Order.Id);
                    break;

                case Target.Cooler:
                    if (_cooler.Count >= _coolerCap) return false;
                    _cooler.Add(orderState.Order.Id);
                    break;

                case Target.Shelf:
                    if (_shelf.Count >= _shelfCap) return false;
                    _shelf.Add(orderState.Order.Id);
                    var shelfRate = TargetExtensions.RateFor(Target.Shelf, orderState.Order.Temp);
                    var expiryTicks = nowUtc.AddSeconds(orderState.BudgetSec / shelfRate).Ticks;
                    _shelfHeap.Enqueue((orderState.Order.Id, TempExtensions.Normalize(orderState.Order.Temp)), expiryTicks);
                    break;
            }

            orderState.Rate = TargetExtensions.RateFor(orderState.Target, orderState.Order.Temp);
            orderState.LastUpdateUtc = nowUtc;
            _states[orderState.Order.Id] = orderState;
            RecordAction(nowUtc, orderState.Order.Id, Domain.Enum.Action.Place, orderState.Target);
            return true;
        }

        private bool TryPlaceOnShelf(OrderState orderState, DateTime nowUtc)
        {
            if (_shelf.Count >= _shelfCap) return false;

            _shelf.Add(orderState.Order.Id);
            orderState.Target = Target.Shelf;
            orderState.Rate = TargetExtensions.RateFor(Target.Shelf, orderState.Order.Temp);
            orderState.LastUpdateUtc = nowUtc;
            _states[orderState.Order.Id] = orderState;

            var shelfRate = orderState.Rate;
            var expiryTicks = nowUtc.AddSeconds(orderState.BudgetSec / shelfRate).Ticks;
            _shelfHeap.Enqueue((orderState.Order.Id, TempExtensions.Normalize(orderState.Order.Temp)), expiryTicks);

            RecordAction(nowUtc, orderState.Order.Id, Domain.Enum.Action.Place, Target.Shelf);
            return true;
        }

        /// <summary>Move earliest-expiring hot/cold from shelf to its ideal if destination has room.</summary>
        private bool TryMoveAnyFromShelfToIdealLocation(DateTime nowUtc)
        {
            var heaterRoom = _heater.Count < _heaterCap;
            var coolerRoom = _cooler.Count < _coolerCap;
            if (!heaterRoom && !coolerRoom) return false;

            var buffer = new List<((string id, string temp) item, long prio)>();
            (string id, string temp) chosen = default;
            var found = false;

            while (_shelfHeap.TryDequeue(out var item, out var prio))
            {
                if (!_shelf.Contains(item.id)) continue; // stale: was picked up/discarded/moved earlier
                var ttemp = TempExtensions.Normalize(item.temp);
                var isHot = ttemp == "hot";
                var isCold = ttemp == "cold";

                if ((isHot && heaterRoom) || (isCold && coolerRoom)) { chosen = item; found = true; break; }
                buffer.Add((item, prio));
            }

            foreach (var (item, prio) in buffer) _shelfHeap.Enqueue(item, prio);
            if (!found) return false;

            if (!_states.TryGetValue(chosen.id, out var s)) return false; // vanished

            s.AccumulateDecay(nowUtc);
            if (s!.BudgetSec <= 0)
            {
                _shelf.Remove(s.Order.Id);
                _states.Remove(s.Order.Id);
                RecordAction(nowUtc, s.Order.Id, Domain.Enum.Action.Discard, Target.Shelf);
                return true;
            }

            _shelf.Remove(s.Order.Id);
            if (TempExtensions.Normalize(chosen.temp) == "hot")
            { _heater.Add(s.Order.Id); s.Target = Target.Heater; }
            else
            { _cooler.Add(s.Order.Id); s.Target = Target.Cooler; }

            s.Rate = TargetExtensions.RateFor(s.Target, s.Order.Temp);
            s.LastUpdateUtc = nowUtc;
            RecordAction(nowUtc, s.Order.Id, Domain.Enum.Action.Move, s.Target);
            return true;
        }

        private void DiscardEarliestFromShelf(DateTime nowUtc)
        {
            while (_shelfHeap.TryDequeue(out var item, out _))
            {
                if (!_shelf.Contains(item.id)) continue; // stale: was picked up/discarded/moved earlier
                if (!_states.TryGetValue(item.id, out var s)) { _shelf.Remove(item.id); continue; }

                s.AccumulateDecay(nowUtc);
                _shelf.Remove(s.Order.Id);
                _states.Remove(s.Order.Id);
                RecordAction(nowUtc, s.Order.Id, Domain.Enum.Action.Discard, Target.Shelf);
                return;
            }
        }

        private void RecordAction(DateTime tsUtc, string id, Domain.Enum.Action action, Target loc)
        {
            var actionName = ActionExtensions.ActionName(action);
            var targetName = TargetExtensions.TargetName(loc);

            var a = new Actions(tsUtc, id, actionName, targetName);
            _actions.Add(a);

            Console.WriteLine($"{a.Timestamp} id={id} action={actionName} target={targetName}");
        }
    }

    
}
