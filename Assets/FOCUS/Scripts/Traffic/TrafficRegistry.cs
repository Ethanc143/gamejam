using System.Collections.Generic;
using UnityEngine;

namespace FocusSim.Traffic
{
    public static class TrafficRegistry
    {
        private static readonly HashSet<TrafficVehicleAI> ActiveVehicles = new HashSet<TrafficVehicleAI>();

        public static IReadOnlyCollection<TrafficVehicleAI> Vehicles => ActiveVehicles;

        public static void Register(TrafficVehicleAI vehicle)
        {
            if (vehicle != null)
            {
                ActiveVehicles.Add(vehicle);
            }
        }

        public static void Unregister(TrafficVehicleAI vehicle)
        {
            if (vehicle != null)
            {
                ActiveVehicles.Remove(vehicle);
            }
        }
    }
}
