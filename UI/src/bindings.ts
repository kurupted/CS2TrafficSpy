import { bindValue, trigger } from "cs2/api";

console.log("[TrafficSpy] Creating bindings...");

// Create bindings with fallback values
export const toolActive = bindValue<boolean>("TrafficSpy", "toolActive", false);
export const activityData = bindValue<string>("TrafficSpy", "activityData", "{}");

// Export the trigger function. 
export const setTrafficFilter = (filter: string) => trigger("TrafficSpy", "setTrafficFilter", filter);

console.log("[TrafficSpy] Bindings created:", { toolActive, activityData });