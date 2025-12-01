import { bindValue, trigger } from "cs2/api";

export const toolActive = bindValue<boolean>("TrafficSpy", "toolActive", false);
export const activityData = bindValue<string>("TrafficSpy", "activityData", "{}");
export const highlightAgents = bindValue<boolean>("TrafficSpy", "highlightAgents", false);
export const showPedestrians = bindValue<boolean>("TrafficSpy", "showPedestrians", false);

export const setTrafficFilter = (filter: string) => trigger("TrafficSpy", "setTrafficFilter", filter);
export const sethighlightAgents = (active: boolean) => trigger("TrafficSpy", "sethighlightAgents", active);
export const setShowPedestrians = (active: boolean) => trigger("TrafficSpy", "setShowPedestrians", active);