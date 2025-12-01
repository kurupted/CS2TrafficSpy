import { bindValue, trigger } from "cs2/api";

export const toolActive = bindValue<boolean>("TrafficSpy", "toolActive", false);
export const activityData = bindValue<string>("TrafficSpy", "activityData", "{}");
export const showAllVehicles = bindValue<boolean>("TrafficSpy", "showAllVehicles", false);
export const showPedestrians = bindValue<boolean>("TrafficSpy", "showPedestrians", false);

export const setTrafficFilter = (filter: string) => trigger("TrafficSpy", "setTrafficFilter", filter);
export const setShowAllVehicles = (active: boolean) => trigger("TrafficSpy", "setShowAllVehicles", active);
export const setShowPedestrians = (active: boolean) => trigger("TrafficSpy", "setShowPedestrians", active);