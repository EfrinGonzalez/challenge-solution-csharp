using Challenge.src.Domain;
using Challenge.src.Domain.Enum;
using Challenge.src.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Actions = Challenge.src.Domain.Actions;

namespace Challenge.src.Services
{
    /// <summary>
    /// Encapsulates all placement/move/discard/pickup rules and state.
    /// Thread-safe via the exposed Sync lock.
    /// </summary>
    public sealed class KitchenManager
    {
        // ---- storage limits ----
        private const int HEATER_CAP = 6, COOLER_CAP = 6, SHELF_CAP = 12;

        // ---- synchronization & state ----
        public object Sync { get; } = new();

        private readonly HashSet<string> _heater = new();
        private readonly HashSet<string> _cooler = new();
        private readonly HashSet<string> _shelf = new();

        // Min-heap keyed by absolute shelf-expiry ticks
        private readonly PriorityQueue<(string id, string temp), long> _shelfHeap = new();

        private readonly Dictionary<string, OrderState> _states = new();

        private readonly List<Actions> _actions = new();
        public List<Actions> Actions => _actions;

        // ---- public API ----
        public void PlaceOrder(Order order, DateTime nowUtc)
        {
            var s = new OrderState
            {
                Order = order,
                StorageLocation = StorageLocationExtensions.IdealFor(order.Temp),
                LastUpdateUtc = nowUtc,
                BudgetSec = order.Freshness, // seconds
                Rate = 1
            };

            // 1) Try ideal location
            if (TryPlaceIdealLocation(s, nowUtc)) return;

            // 2) Try shelf
            if (TryPlaceOnShelf(s, nowUtc)) return;

            // 3) Shelf full → try to move any hot/cold from shelf to its ideal (if heater/cooler has room)
            if (TryMoveAnyFromShelfToIdealLocation(nowUtc))
            {
                if (TryPlaceOnShelf(s, nowUtc)) return;
            }
            else
            {
                // 4) Discard earliest shelf item to make room
                DiscardEarliestFromShelf(nowUtc);
                if (TryPlaceOnShelf(s, nowUtc)) return;
            }

            throw new InvalidOperationException("Placement failed even after making room.");
        }

        public void PickupOrder(string id, DateTime nowUtc)
        {
            if (!_states.TryGetValue(id, out var s)) return; // not present → no-op

            //AccumulateDecay(s, nowUtc);
            s.AccumulateDecay(nowUtc);
            if (s.BudgetSec <= 0)
            {
                // discard expired
                switch (s.StorageLocation)
                {
                    case StorageLocation.Heater: _heater.Remove(id); break;
                    case StorageLocation.Cooler: _cooler.Remove(id); break;
                    case StorageLocation.Shelf: _shelf.Remove(id); break; // heap is lazy
                }
                _states.Remove(id);
                AppendLedgerEntry(nowUtc, id, KitchenAction.Discard, s.StorageLocation);
                return;
            }

            // pickup
            switch (s.StorageLocation)
            {
                case StorageLocation.Heater: _heater.Remove(id); break;
                case StorageLocation.Cooler: _cooler.Remove(id); break;
                case StorageLocation.Shelf: _shelf.Remove(id); break; // heap is lazy
            }
            _states.Remove(id);
            AppendLedgerEntry(nowUtc, id, KitchenAction.Pickup, s.StorageLocation);
        }

        // ---- helpers (private) ----
        private bool TryPlaceIdealLocation(OrderState s, DateTime nowUtc)
        {
            switch (s.StorageLocation)
            {
                case StorageLocation.Heater:
                    if (_heater.Count >= HEATER_CAP) return false;
                    _heater.Add(s.Order.Id);
                    break;

                case StorageLocation.Cooler:
                    if (_cooler.Count >= COOLER_CAP) return false;
                    _cooler.Add(s.Order.Id);
                    break;

                case StorageLocation.Shelf:
                    if (_shelf.Count >= SHELF_CAP) return false;
                    _shelf.Add(s.Order.Id);
                    var shelfRate = StorageLocationExtensions.RateFor(StorageLocation.Shelf, s.Order.Temp);
                    var expiryTicks = nowUtc.AddSeconds(s.BudgetSec / shelfRate).Ticks;
                    _shelfHeap.Enqueue((s.Order.Id, TempExtensions.Normalize(s.Order.Temp)), expiryTicks);
                    break;
            }

            s.Rate = StorageLocationExtensions.RateFor(s.StorageLocation, s.Order.Temp);
            s.LastUpdateUtc = nowUtc;
            _states[s.Order.Id] = s;
            AppendLedgerEntry(nowUtc, s.Order.Id, KitchenAction.Place, s.StorageLocation);
            return true;
        }

        private bool TryPlaceOnShelf(OrderState s, DateTime nowUtc)
        {
            if (_shelf.Count >= SHELF_CAP) return false;

            _shelf.Add(s.Order.Id);
            s.StorageLocation = StorageLocation.Shelf;
            s.Rate = StorageLocationExtensions.RateFor(StorageLocation.Shelf, s.Order.Temp);
            s.LastUpdateUtc = nowUtc;
            _states[s.Order.Id] = s;

            var shelfRate = s.Rate;
            var expiryTicks = nowUtc.AddSeconds(s.BudgetSec / shelfRate).Ticks;
            _shelfHeap.Enqueue((s.Order.Id, TempExtensions.Normalize(s.Order.Temp)), expiryTicks);

            AppendLedgerEntry(nowUtc, s.Order.Id, KitchenAction.Place, StorageLocation.Shelf);
            return true;
        }

        /// <summary>Move earliest-expiring hot/cold from shelf to its ideal if destination has room.</summary>
        private bool TryMoveAnyFromShelfToIdealLocation(DateTime nowUtc)
        {
            var heaterRoom = _heater.Count < HEATER_CAP;
            var coolerRoom = _cooler.Count < COOLER_CAP;
            if (!heaterRoom && !coolerRoom) return false;

            var buffer = new List<((string id, string temp) item, long prio)>();
            (string id, string temp) chosen = default;
            var found = false;

            while (_shelfHeap.TryDequeue(out var item, out var prio))
            {
                if (!_shelf.Contains(item.id)) continue; // stale
                var ttemp = TempExtensions.Normalize(item.temp);
                var isHot = ttemp == "hot";
                var isCold = ttemp == "cold";

                if ((isHot && heaterRoom) || (isCold && coolerRoom)) { chosen = item; found = true; break; }
                buffer.Add((item, prio));
            }

            foreach (var b in buffer) _shelfHeap.Enqueue(b.item, b.prio);
            if (!found) return false;

            if (!_states.TryGetValue(chosen.id, out var s)) return false; // vanished

            s.AccumulateDecay(nowUtc);
            if (s!.BudgetSec <= 0)
            {
                _shelf.Remove(s.Order.Id);
                _states.Remove(s.Order.Id);
                AppendLedgerEntry(nowUtc, s.Order.Id, KitchenAction.Discard, StorageLocation.Shelf);
                return true;
            }

            _shelf.Remove(s.Order.Id);
            if (TempExtensions.Normalize(chosen.temp) == "hot")
            { _heater.Add(s.Order.Id); s.StorageLocation = StorageLocation.Heater; }
            else
            { _cooler.Add(s.Order.Id); s.StorageLocation = StorageLocation.Cooler; }

            s.Rate = StorageLocationExtensions.RateFor(s.StorageLocation, s.Order.Temp);
            s.LastUpdateUtc = nowUtc;
            AppendLedgerEntry(nowUtc, s.Order.Id, KitchenAction.Move, s.StorageLocation);
            return true;
        }

        private void DiscardEarliestFromShelf(DateTime nowUtc)
        {
            while (_shelfHeap.TryDequeue(out var item, out _))
            {
                if (!_shelf.Contains(item.id)) continue; // stale
                if (!_states.TryGetValue(item.id, out var s)) { _shelf.Remove(item.id); continue; }

                //AccumulateDecay(s, nowUtc);
                s.AccumulateDecay(nowUtc);
                _shelf.Remove(s.Order.Id);
                _states.Remove(s.Order.Id);
                AppendLedgerEntry(nowUtc, s.Order.Id, KitchenAction.Discard, StorageLocation.Shelf);
                return;
            }
        }

        private void AppendLedgerEntry(DateTime tsUtc, string id, KitchenAction action, StorageLocation loc)
        {
            var actionName = KitchenActionExtensions.ActionName(action);
            var targetName = StorageLocationExtensions.TargetName(loc);

            var a = new Actions(tsUtc, id, actionName, targetName);
            _actions.Add(a);

            Console.WriteLine($"{a.Timestamp} id={id} action={actionName} target={targetName}");
        }
    }

    
}
