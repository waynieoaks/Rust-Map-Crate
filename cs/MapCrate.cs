using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MapCrate", "Waynieoaks", "1.0.0")]
    [Description("Places a small blue ring on the map for Hackable Locked Crates (excludes Cargo Ship).")]
    public class MapCrate : RustPlugin
    {
        // Keep it simple: one toggle + ring size
        private const bool DEBUG = false;
        private const float RING_RADIUS = 0.5f;       // Small ring so it's distinct from airdrops
        private const float RING_ALPHA  = 0.6f;
        private const float CARGO_NEAR_DIST = 120f;   // If crate is within this of a Cargo Ship, we ignore it

        // Track markers so we can remove them cleanly
        private readonly Dictionary<ulong, MapMarkerGenericRadius> _markers = new Dictionary<ulong, MapMarkerGenericRadius>();

        #region Hooks

        private void OnServerInitialized()
        {
            // On plugin load / server restart, add rings for any existing land-based hackable crates
            foreach (var crate in UnityEngine.Object.FindObjectsOfType<HackableLockedCrate>())
            {
                TryMarkCrate(crate);
            }
        }

        // Use the BaseNetworkable variant so it always fires
        private void OnEntitySpawned(HackableLockedCrate crate)
        {
            if (crate == null || !crate.IsValid()) return;

            TryMarkCrate(crate);
        }

        private void OnEntityKill(HackableLockedCrate crate)
        {
            if (crate == null || !crate.IsValid()) return;

            var id = crate.net?.ID.Value ?? 0UL;
            if (id == 0UL) return;

            if (_markers.TryGetValue(id, out var marker))
            {
                if (DEBUG) Puts($"[MapCrate] Removing marker for crate {id}");
                if (marker != null && !marker.IsDestroyed) marker.Kill();
                _markers.Remove(id);
            }
        }

        private void Unload()
        {
            // Clean up all rings we created
            foreach (var kv in _markers)
            {
                var marker = kv.Value;
                if (marker != null && !marker.IsDestroyed) marker.Kill();
            }
            _markers.Clear();
        }

        #endregion

        #region Core

        private void TryMarkCrate(HackableLockedCrate crate)
        {
            if (crate == null || !crate.IsValid()) return;
            var id = crate.net?.ID.Value ?? 0UL;
            if (id == 0UL) return;

            // Skip if near Cargo Ship
            if (IsNearCargoShip(crate.transform.position, CARGO_NEAR_DIST))
            {
                if (DEBUG) Puts($"[MapCrate] Skipping crate {id} (near Cargo Ship)");
                return;
            }

            // Already marked?
            if (_markers.ContainsKey(id)) return;

            // Create a small blue radius marker
            var marker = GameManager.server.CreateEntity(
                "assets/prefabs/tools/map/genericradiusmarker.prefab",
                crate.transform.position) as MapMarkerGenericRadius;

            if (marker == null) return;

            marker.alpha = RING_ALPHA;
            marker.color1 = Color.blue;
            marker.radius = RING_RADIUS;
            marker.enableSaving = true;

            marker.Spawn();
            marker.SendUpdate();
            marker.SendNetworkUpdateImmediate();

            _markers[id] = marker;

            if (DEBUG) Puts($"[MapCrate] Marker created for crate {id} at {crate.transform.position}");
        }

        private bool IsNearCargoShip(Vector3 pos, float maxDist)
        {
            // Simple distance check to any active CargoShip
            foreach (var ship in UnityEngine.Object.FindObjectsOfType<CargoShip>())
            {
                // Cargo is large and moves; distance-based ignore is sufficient/KISS
                if (Vector3.Distance(ship.transform.position, pos) <= maxDist)
                    return true;
            }
            return false;
        }
		
		private void OnPlayerConnected(BasePlayer player)
		{
			timer.Once(2f, () =>
			{
				if (player == null || !player.IsConnected) return;

				foreach (var marker in _markers.Values)
				{
					if (marker != null && !marker.IsDestroyed)
					{
						marker.SendUpdate();
						marker.SendNetworkUpdateImmediate();
					}
				}
			});
		}

        #endregion
    }
}
