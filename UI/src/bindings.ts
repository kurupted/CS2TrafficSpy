import { bindValue } from "cs2/api";

console.log("[TrafficSpy] Creating bindings...");

// Create bindings with fallback values
export const toolActive = bindValue<boolean>("TrafficSpy", "toolActive", false);
export const segmentActivity = bindValue<string>("TrafficSpy", "segmentActivity", "{}");

console.log("[TrafficSpy] Bindings created:", { toolActive, segmentActivity });