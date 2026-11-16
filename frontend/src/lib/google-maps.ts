/** Narrow interfaces for Maps and Advanced Markers; no GPS/tracking API. */
export type Position = { lat: number; lng: number };
export interface Bounds {
  extend(position: Position): void;
}
export interface MapView {
  getMapCapabilities(): { isAdvancedMarkersAvailable?: boolean };
  addListener(event: string, callback: () => void): { remove(): void };
  fitBounds(bounds: Bounds, padding?: number): void;
  setCenter(position: Position): void;
  setZoom(zoom: number): void;
}
export interface Marker {
  map: MapView | null;
  appendChild(element: HTMLElement): void;
  addEventListener(event: string, callback: () => void): void;
  removeEventListener(event: string, callback: () => void): void;
}
export interface InfoWindow {
  setContent(content: HTMLElement): void;
  open(options: { map: MapView; anchor: Marker; shouldFocus: boolean }): void;
  close(): void;
}
export interface MapsLibrary {
  Map: new (
    element: HTMLElement,
    options: {
      center: Position;
      zoom: number;
      mapId: string;
      gestureHandling: string;
      colorScheme: "LIGHT";
    },
  ) => MapView;
  InfoWindow: new () => InfoWindow;
}
export interface MarkerLibrary {
  AdvancedMarkerElement: new (options: {
    map: MapView;
    position: Position;
    title: string;
    gmpClickable: boolean;
  }) => Marker;
}
export interface CoreLibrary {
  LatLngBounds: new () => Bounds;
}
type GoogleMaps = { importLibrary(name: string): Promise<unknown> };
declare global {
  interface Window {
    google?: { maps: GoogleMaps };
    serviceOpsMapsReady?: () => void;
    gm_authFailure?: () => void;
  }
}
let loading: Promise<GoogleMaps> | undefined;
let failedAuthentication = false;
const message =
  "Google Maps could not load. Check its credentials, billing, map ID and network access.";
/** One script/promise per page. Retries of transport failures never duplicate an active load. */
export function loadGoogleMaps(key: string): Promise<GoogleMaps> {
  if (failedAuthentication) return Promise.reject(new Error(message));
  if (loading) return loading;
  if (window.google?.maps.importLibrary)
    return Promise.resolve(window.google.maps);
  loading = new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.id = "serviceops-google-maps";
    let settled = false;
    const cleanup = () => {
      clearTimeout(timeout);
      delete window.serviceOpsMapsReady;
      script.onerror = null;
    };
    const fail = () => {
      window.dispatchEvent(new Event("serviceops-maps-error"));
      if (settled) return;
      settled = true;
      cleanup();
      script.remove();
      loading = undefined;
      reject(new Error(message));
    };
    const timeout = window.setTimeout(fail, 20000);
    window.serviceOpsMapsReady = () => {
      if (!window.google?.maps.importLibrary) {
        fail();
        return;
      }
      settled = true;
      cleanup();
      resolve(window.google.maps);
    };
    window.gm_authFailure = () => {
      failedAuthentication = true;
      fail();
    };
    script.onerror = fail;
    script.async = true;
    script.src =
      "https://maps.googleapis.com/maps/api/js?" +
      new URLSearchParams({
        key,
        v: "weekly",
        loading: "async",
        callback: "serviceOpsMapsReady",
      });
    document.head.append(script);
  });
  return loading;
}
