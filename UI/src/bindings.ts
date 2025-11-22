import { bindValue } from "cs2/api";

// Add fallback values (false for boolean, empty JSON for string)
export const toolActive = bindValue<boolean>("TrafficSpy", "toolActive", false);
export const segmentActivity = bindValue<string>("TrafficSpy", "segmentActivity", "{}");
