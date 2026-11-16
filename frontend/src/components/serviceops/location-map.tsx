"use client";
import { useEffect, useMemo, useState } from "react";
import { MapPin, Wrench, ExternalLink } from "lucide-react";
import { api } from "@/lib/serviceops-api";
import {
  LocationMapCanvas,
  MapErrorBoundary,
  hasCoordinates,
  type LocationPin,
} from "./location-map-canvas";
import styles from "./location-map.module.css";

export function LocationMap() {
  const [pins, setPins] = useState<LocationPin[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState("");
  const [type, setType] = useState("all");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const key = process.env.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY?.trim();
  const mapId = process.env.NEXT_PUBLIC_GOOGLE_MAPS_MAP_ID?.trim();
  useEffect(() => {
    let active = true;
    api("locations")
      .then((data) => {
        if (active) setPins(data);
      })
      .catch(() => {
        if (active)
          setError(
            "Authorized locations could not be retrieved. Please refresh or sign in again.",
          );
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);
  const types = useMemo(
    () => [...new Set(pins.map((pin) => pin.locationType))],
    [pins],
  );
  const visible = useMemo(
    () =>
      pins.filter(
        (pin) =>
          (type === "all" || pin.locationType === type) &&
          `${pin.name} ${pin.address} ${pin.technicianName ?? ""} ${pin.workOrderId ?? ""}`
            .toLowerCase()
            .includes(filter.trim().toLowerCase()),
      ),
    [pins, filter, type],
  );
  const positions = useMemo(() => visible.filter(hasCoordinates), [visible]);
  const selected = visible.some((pin) => pin.id === selectedId)
    ? selectedId
    : null;
  return (
    <section className="panel" aria-label="Authorized service locations">
      <h2>Service locations</h2>
      <p className="muted">
        Registered customer sites and assigned service locations. These pins are
        not live technician tracking.
      </p>
      <div className={styles.filters}>
        <label>
          Search authorized locations
          <input value={filter} onChange={(e) => setFilter(e.target.value)} />
        </label>
        <label>
          Location type
          <select
            aria-label="Location type"
            value={type}
            onChange={(e) => setType(e.target.value)}
          >
            <option value="all">All permitted locations</option>
            {types.map((value) => (
              <option key={value} value={value}>
                {value === "Customer Service Location"
                  ? "Customer service sites"
                  : value}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className={styles.legend}>
        {types.includes("Customer Service Location") ? (
          <span className={styles.siteLegend}>
            <MapPin size={16} /> Customer service site
          </span>
        ) : null}
        {types.includes("Assigned Service Location") ? (
          <span className={styles.technicianLegend}>
            <Wrench size={16} /> Assigned Service Location
          </span>
        ) : null}
      </div>
      {loading ? (
        <div className={styles.skeleton} role="status">
          Loading authorized locations…
        </div>
      ) : (
        <>
          {error ? (
            <p className="error" role="alert">
              {error}
            </p>
          ) : null}
          {!key || !mapId ? (
            <p className={styles.fallback} role="status">
              Google Maps is unavailable: a restricted API key and map ID have
              not been configured. Authorized addresses remain available below.
            </p>
          ) : null}
          {key && mapId && positions.length > 0 ? (
            <MapErrorBoundary>
              <LocationMapCanvas
                apiKey={key}
                mapId={mapId}
                pins={positions}
                selectedId={selected}
                onSelect={setSelectedId}
              />
            </MapErrorBoundary>
          ) : null}
          {!visible.length && !error ? (
            <p>No authorized locations match this selection.</p>
          ) : null}
          {visible.length > 0 && !positions.length ? (
            <p className={styles.fallback}>
              These locations have no registered coordinates. Address
              information remains available.
            </p>
          ) : null}
          <p className="muted" role="status">
            {visible.length} authorized locations · {positions.length} with
            registered coordinates
          </p>
          <ul className={styles.list} aria-label="Authorized location list">
            {visible.map((pin) => (
              <li
                key={pin.id}
                className={
                  selected === pin.id ? styles.selectedLocation : undefined
                }
              >
                <button
                  className={styles.locationButton}
                  type="button"
                  aria-pressed={selected === pin.id}
                  onClick={() => setSelectedId(pin.id)}
                >
                  <strong>{pin.name}</strong>
                  <span>
                    {pin.locationType}
                    {pin.workOrderId ? ` · Work order #${pin.workOrderId}` : ""}
                  </span>
                </button>
                <p>{pin.address}</p>
                {pin.technicianName ? (
                  <p>Assigned technician: {pin.technicianName}</p>
                ) : null}
                <small>
                  {hasCoordinates(pin)
                    ? `${pin.latitude}, ${pin.longitude}`
                    : "Coordinates not registered; address only."}
                </small>
                {hasCoordinates(pin) ? (
                  <a
                    className={styles.externalLink}
                    href={`https://www.google.com/maps/search/?${new URLSearchParams({ api: "1", query: `${pin.latitude},${pin.longitude}` })}`}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Open in Google Maps{" "}
                    <ExternalLink size={14} aria-hidden="true" />
                    <span className="sr-only"> (opens a new tab)</span>
                  </a>
                ) : null}
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
