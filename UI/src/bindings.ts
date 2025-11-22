import { bindValue } from "cs2/api";

// We create the binding object once. 
// "trafficExplorer" is the group name we defined in TrafficUISystem.cs
export const toolActive = bindValue<boolean>("trafficExplorer", "toolActive");
export const segmentActivity = bindValue<string>("trafficExplorer", "segmentActivity");