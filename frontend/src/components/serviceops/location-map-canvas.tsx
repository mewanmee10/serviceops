"use client";
import { Component, useEffect, useRef, useState, type ReactNode } from "react";
import {
  loadGoogleMaps,
  type CoreLibrary,
  type MapsLibrary,
  type MarkerLibrary,
  type MapView,
  type InfoWindow,
} from "@/lib/google-maps";
import styles from "./location-map.module.css";

export type LocationPin = {
  id: string;
  siteId: number;
  name: string;
  address: string;
  latitude: number | null;
  longitude: number | null;
  locationType: string;
  workOrderId: number | null;
  technicianName: string | null;
};
export function hasCoordinates(pin: LocationPin): boolean {
  return (
    pin.latitude !== null &&
    pin.longitude !== null &&
    Number.isFinite(pin.latitude) &&
    Number.isFinite(pin.longitude) &&
    Math.abs(pin.latitude) <= 90 &&
    Math.abs(pin.longitude) <= 180
  );
}
export class MapErrorBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <p className="error" role="alert">
        The interactive map is unavailable. Authorized addresses remain
        available below.
      </p>
    ) : (
      this.props.children
    );
  }
}
function information(pin: LocationPin) {
  const content = document.createElement("div");
  content.className = styles.info;
  for (const text of [
    pin.name,
    pin.locationType,
    pin.address,
    `${pin.latitude}, ${pin.longitude}`,
    pin.technicianName,
    pin.workOrderId ? `Work order #${pin.workOrderId}` : null,
  ]) {
    if (!text) continue;
    const paragraph = document.createElement("p");
    paragraph.textContent = text;
    content.append(paragraph);
  }
  return content;
}
type Runtime = {
  map: MapView;
  info: InfoWindow;
  marker: MarkerLibrary;
  core: CoreLibrary;
};
export function LocationMapCanvas({
  apiKey,
  mapId,
  pins,
  selectedId,
  onSelect,
}: {
  apiKey: string;
  mapId: string;
  pins: LocationPin[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}) {
  const canvas = useRef<HTMLDivElement>(null);
  const first = useRef({ lat: pins[0].latitude!, lng: pins[0].longitude! });
  const [runtime, setRuntime] = useState<Runtime | null>(null);
  const [error, setError] = useState("");
  const [ready, setReady] = useState(false);
  const selection = useRef<((id: string | null) => void) | null>(null);
  useEffect(() => {
    let active = true;
    let failed = false;
    let info: InfoWindow | undefined;
    let tiles: { remove(): void } | undefined;
    let capabilities: { remove(): void } | undefined;
    const fail = () => {
      if (active) {
        failed = true;
        clearTimeout(deadline);
        setError(
          "Google Maps is unavailable. Check the API key, provider quota, billing, referrer restrictions, map ID and network connection.",
        );
      }
    };
    // SDK callbacks/Map construction do not prove that usable tiles rendered.
    // Bound library and tile loading as well as the script loader's own deadline.
    const deadline = window.setTimeout(fail, 25000);
    window.addEventListener("serviceops-maps-error", fail);
    loadGoogleMaps(apiKey)
      .then(async (google) => {
        const [maps, marker, core] = (await Promise.all([
          google.importLibrary("maps"),
          google.importLibrary("marker"),
          google.importLibrary("core"),
        ])) as [MapsLibrary, MarkerLibrary, CoreLibrary];
        if (!active || failed || !canvas.current) return;
        const map = new maps.Map(canvas.current, {
          center: first.current,
          zoom: 13,
          mapId,
          gestureHandling: "cooperative",
          colorScheme: "LIGHT",
        });
        tiles = map.addListener("tilesloaded", () => {
          if (active && !failed) {
            clearTimeout(deadline);
            setReady(true);
          }
        });
        capabilities = map.addListener("mapcapabilities_changed", () => {
          if (active && !map.getMapCapabilities().isAdvancedMarkersAvailable)
            setError(
              "Google Maps cannot display advanced markers. Check that the configured map ID belongs to this Google Cloud project and supports JavaScript maps.",
            );
        });
        info = new maps.InfoWindow();
        setRuntime({ map, info, marker, core });
      })
      .catch(fail);
    return () => {
      active = false;
      clearTimeout(deadline);
      tiles?.remove();
      window.removeEventListener("serviceops-maps-error", fail);
      info?.close();
      capabilities?.remove();
    };
  }, [apiKey, mapId]);
  useEffect(() => {
    if (!runtime) return;
    const { map, info, marker, core } = runtime;
    const bounds = new core.LatLngBounds();
    const entries = pins.map((pin) => {
      const position = { lat: pin.latitude!, lng: pin.longitude! };
      bounds.extend(position);
      const badge = document.createElement("span");
      badge.className =
        pin.locationType === "Assigned Service Location"
          ? styles.technicianPin
          : styles.sitePin;
      badge.textContent =
        pin.locationType === "Assigned Service Location" ? "T" : "S";
      const item = new marker.AdvancedMarkerElement({
        map,
        position,
        title: `${pin.name} — ${pin.locationType}`,
        gmpClickable: true,
      });
      item.appendChild(badge);
      const click = () => onSelect(pin.id);
      item.addEventListener("gmp-click", click);
      return { pin, item, badge, click };
    });
    const select = (id: string | null) => {
      info.close();
      for (const entry of entries)
        entry.badge.classList.toggle(styles.selectedPin, entry.pin.id === id);
      const entry = entries.find((entry) => entry.pin.id === id);
      if (entry) {
        map.setCenter({ lat: entry.pin.latitude!, lng: entry.pin.longitude! });
        map.setZoom(14);
        info.setContent(information(entry.pin));
        info.open({ map, anchor: entry.item, shouldFocus: false });
      }
    };
    selection.current = select;
    if (pins.length === 1) {
      map.setCenter({ lat: pins[0].latitude!, lng: pins[0].longitude! });
      map.setZoom(14);
    } else if (pins.length > 1) map.fitBounds(bounds, 55);
    return () => {
      info.close();
      for (const entry of entries) {
        entry.item.removeEventListener("gmp-click", entry.click);
        entry.item.map = null;
      }
    };
  }, [runtime, pins, onSelect]);
  useEffect(() => {
    selection.current?.(selectedId);
  }, [runtime, pins, selectedId]);
  return (
    <div className={styles.mapShell}>
      {!ready && !error ? (
        <div className={styles.skeleton} role="status">
          Loading interactive Google Map…
        </div>
      ) : null}
      {error ? (
        <p className="error" role="alert">
          {error} Authorized addresses remain available below.
        </p>
      ) : null}
      <div
        ref={canvas}
        className={styles.map}
        hidden={!!error}
        aria-label="Google Map of authorized service locations"
      />
    </div>
  );
}
