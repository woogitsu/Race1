using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>Keeps one visual car per traffic car, creating and removing them as the simulation spawns and recycles.</summary>
    public sealed class TrafficView
    {
        private static readonly Color[] Palette =
        {
            new Color(0.85f, 0.85f, 0.87f), new Color(0.08f, 0.09f, 0.11f), new Color(0.55f, 0.12f, 0.12f),
            new Color(0.12f, 0.27f, 0.45f), new Color(0.7f, 0.66f, 0.5f), new Color(0.2f, 0.45f, 0.3f),
        };

        private readonly Transform _parent;
        private readonly VehicleSpec _spec;
        private readonly Dictionary<int, CarView> _views = new Dictionary<int, CarView>();
        private readonly HashSet<int> _seen = new HashSet<int>();

        public TrafficView(Transform parent, VehicleSpec spec)
        {
            _parent = parent;
            _spec = spec;
        }

        public void Tick(IReadOnlyList<TrafficCar> cars, RoadTrack road, Vector3d origin)
        {
            _seen.Clear();
            for (int i = 0; i < cars.Count; i++)
            {
                TrafficCar car = cars[i];
                _seen.Add(car.Id);
                if (!_views.TryGetValue(car.Id, out CarView view))
                {
                    Color paint = Palette[car.Id % Palette.Length];
                    view = CarView.Create(_parent, _spec, paint, $"Traffic_{car.Id}");
                    _views[car.Id] = view;
                }
                RoadSample sample = road.Sample(car.S);
                view.Place(sample, car.T, 0.0, origin, null, 0f);
            }

            var gone = new List<int>();
            foreach (KeyValuePair<int, CarView> pair in _views)
            {
                if (!_seen.Contains(pair.Key)) gone.Add(pair.Key);
            }
            foreach (int id in gone)
            {
                Object.Destroy(_views[id].gameObject);
                _views.Remove(id);
            }
        }

        public void Clear()
        {
            foreach (CarView v in _views.Values) Object.Destroy(v.gameObject);
            _views.Clear();
        }
    }
}
