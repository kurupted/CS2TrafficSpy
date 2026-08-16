import React, { useMemo } from "react";
import { useValue } from "cs2/api";
import {
    trafficJamData,
    focusEntityTrigger
} from "./bindings";

interface NotificationItem {
    index: number;
    version: number;
    iconIndex?: number;
    iconVersion?: number;
    name: string;
}

interface BlockerItem {
    index: number;
    version: number;
    name: string;
    type: string;
    waitingCount: number;
    reason: "signal" | "stopped" | "boarding";
}

interface TrafficMonitorPanelProps {
    onClose?: () => void;
}

const TrafficJamAlertIcon: React.FC = () => (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#ff4d4d" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" fill="rgba(255, 77, 77, 0.2)" />
        <line x1="12" y1="9" x2="12" y2="13" stroke="#ff4d4d" strokeWidth="2.5" />
        <line x1="12" y1="17" x2="12.01" y2="17" stroke="#ff4d4d" strokeWidth="3" />
    </svg>
);

const BusIcon: React.FC = () => (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#52b8ff" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <rect x="3" y="3" width="18" height="14" rx="3" />
        <path d="M4 10h16" />
        <circle cx="7.5" cy="14" r="1" fill="#52b8ff" />
        <circle cx="16.5" cy="14" r="1" fill="#52b8ff" />
        <path d="M6 17v2" />
        <path d="M18 17v2" />
    </svg>
);

const TaxiIcon: React.FC = () => (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#ffdb58" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M9 3h6v2H9z" fill="#ffdb58" />
        <path d="M5 10l2-4h10l2 4v6H5z" />
        <circle cx="7.5" cy="16" r="1.2" fill="#ffdb58" />
        <circle cx="16.5" cy="16" r="1.2" fill="#ffdb58" />
    </svg>
);

const VanIcon: React.FC = () => (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#ffaa55" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <rect x="2" y="6" width="13" height="9" rx="1.5" />
        <path d="M15 9h4l2 3v3h-6V9z" />
        <circle cx="6" cy="15" r="1.2" fill="#ffaa55" />
        <circle cx="17" cy="15" r="1.2" fill="#ffaa55" />
    </svg>
);

const CarIcon: React.FC = () => (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#66cc99" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M5 10l2-4h10l2 4v5H5z" />
        <circle cx="7.5" cy="15" r="1.2" fill="#66cc99" />
        <circle cx="16.5" cy="15" r="1.2" fill="#66cc99" />
    </svg>
);

const SignalIcon: React.FC = () => (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
        <rect x="7" y="2" width="10" height="20" rx="5" fill="rgba(20,20,20,0.9)" stroke="rgba(255,255,255,0.4)" strokeWidth="1.5" />
        <circle cx="12" cy="6" r="2.2" fill="#ff4444" />
        <circle cx="12" cy="12" r="2.2" fill="#ffbb00" />
        <circle cx="12" cy="18" r="2.2" fill="#00cc66" />
    </svg>
);

const StoppedIcon: React.FC = () => (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#ffaa00" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
    </svg>
);

const BoardingIcon: React.FC = () => (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#ffaa22" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
        <circle cx="9" cy="7" r="4" />
        <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
        <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
);

const LocationPinIcon: React.FC = () => (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#52b8ff" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
        <circle cx="12" cy="10" r="3" />
    </svg>
);

const CloseIcon: React.FC = () => (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#ffffff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <line x1="18" y1="6" x2="6" y2="18" />
        <line x1="6" y1="6" x2="18" y2="18" />
    </svg>
);

const MaintenanceIcon: React.FC = () => (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#ffaa22" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <rect x="2" y="6" width="13" height="9" rx="1.5" />
        <path d="M15 9h4l2 3v3h-6V9z" />
        <circle cx="6" cy="15" r="1.2" fill="#ffaa22" />
        <circle cx="17" cy="15" r="1.2" fill="#ffaa22" />
        <path d="M6 10h4" />
    </svg>
);

const HeaderBusIcon: React.FC = () => (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#52b8ff" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <rect x="3" y="3" width="18" height="14" rx="3" />
        <path d="M4 10h16" />
        <circle cx="7.5" cy="14" r="1.2" fill="#52b8ff" />
        <circle cx="16.5" cy="14" r="1.2" fill="#52b8ff" />
        <path d="M6 17v2" />
        <path d="M18 17v2" />
    </svg>
);

const cleanDisplayName = (name: string): string => {
    if (!name) return "";
    let cleaned = name.trim();
    const bracketMatch = cleaned.match(/\[(.*?)\]/);
    if (bracketMatch) {
        cleaned = bracketMatch[1];
    }
    cleaned = cleaned.replace(/^(Assets|SelectedInfoPanel|Common|Notification|SubNet|Net)\./i, "");
    cleaned = cleaned.replace(/^(NAME|ASSET_NAME|STREET_NAME|ROAD_NAME|SECTION_NAME)[:_ ]*/i, "");
    return cleaned.replace(/_/g, " ").trim();
};

export const TrafficMonitorPanel: React.FC<TrafficMonitorPanelProps> = ({ onClose }) => {
    const rawJamData = useValue(trafficJamData);

    const { notifications, blockers } = useMemo(() => {
        try {
            const parsed = JSON.parse(rawJamData || "{}");
            if (Array.isArray(parsed)) {
                const list = [...parsed].sort((a: BlockerItem, b: BlockerItem) => {
                    const cmp = (b.waitingCount || 0) - (a.waitingCount || 0);
                    if (cmp !== 0) return cmp;
                    return a.index - b.index;
                }).slice(0, 15);
                return { notifications: [], blockers: list };
            }
            const notifs: NotificationItem[] = parsed.notifications || [];
            const blks: BlockerItem[] = (parsed.blockers || []).sort((a: BlockerItem, b: BlockerItem) => {
                const cmp = (b.waitingCount || 0) - (a.waitingCount || 0);
                if (cmp !== 0) return cmp;
                return a.index - b.index;
            }).slice(0, 15);
            return { notifications: notifs, blockers: blks };
        } catch (e) {
            return { notifications: [], blockers: [] };
        }
    }, [rawJamData]);

    const handleLocationClick = (index: number, version: number) => {
        const entity = { index, version };
        try {
            focusEntityTrigger(entity);
            // Move camera to vehicle / road segment / notification without opening properties panel
        } catch (e) {
            console.error("[TrafficSpy] Error triggering focus:", e);
        }
    };

    const renderVehicleIcon = (type: string, name: string) => {
        const lowerName = name.toLowerCase();
        const lowerType = (type || "").toLowerCase();
        if (lowerType.includes("maintenance") || lowerName.includes("maintenance")) return <MaintenanceIcon />;
        if (lowerName.includes("bus") || lowerType.includes("bus")) return <BusIcon />;
        if (lowerName.includes("taxi") || lowerType.includes("taxi")) return <TaxiIcon />;
        if (lowerName.includes("van") || lowerName.includes("delivery") || lowerName.includes("truck") || lowerType.includes("van")) return <VanIcon />;
        return <CarIcon />;
    };

    return (
        <div style={{
            position: "absolute",
            top: "100rem",
            left: "20rem",
            width: "340rem",
            maxHeight: "520rem",
            backgroundColor: "rgba(22, 30, 42, 0.94)",
            backdropFilter: "blur(10px)",
            border: "1px solid rgba(120, 200, 255, 0.2)",
            borderRadius: "8rem",
            boxShadow: "0 8rem 24rem rgba(0, 0, 0, 0.6)",
            color: "#ffffff",
            fontFamily: "var(--font-family-body, 'Inter', sans-serif)",
            display: "flex",
            flexDirection: "column",
            zIndex: 9999,
            overflow: "hidden",
            pointerEvents: "auto"
        }}>
            {/* Window Header */}
            <div style={{
                display: "flex",
                flexDirection: "row",
                alignItems: "center",
                justifyContent: "space-between",
                padding: "12rem 16rem",
                borderBottom: "1px solid rgba(255, 255, 255, 0.1)",
                backgroundColor: "rgba(10, 16, 26, 0.5)"
            }}>
                <div style={{ display: "flex", flexDirection: "row", alignItems: "center", gap: "14rem" }}>
                    <HeaderBusIcon />
                    <span style={{
                        fontSize: "14rem",
                        fontWeight: 700,
                        color: "#52b8ff",
                        letterSpacing: "0.5rem",
                        textTransform: "uppercase",
                        marginLeft: "3rem"
                    }}>
                        Traffic Jam Monitor
                    </span>
                </div>
                {onClose && (
                    <div
                        onClick={onClose}
                        style={{
                            cursor: "pointer",
                            color: "#ffffff",
                            padding: "4rem 4rem",
                            borderRadius: "4rem",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            lineHeight: 1
                        }}
                        title="Close"
                    >
                        <CloseIcon />
                    </div>
                )}
            </div>

            {/* List Body */}
            <div style={{
                flex: 1,
                overflowY: "auto",
                maxHeight: "440rem",
                padding: "6rem 8rem 8rem 8rem"
            }}>
                {/* 1. In-Game Traffic Jam Notifications (if any active) */}
                {notifications.length > 0 && (
                    <div style={{ marginBottom: "8rem" }}>
                        <div style={{
                            padding: "4rem 8rem 6rem 8rem",
                            fontSize: "12rem",
                            fontWeight: 700,
                            color: "#ff7777",
                            textTransform: "uppercase",
                            letterSpacing: "0.5rem",
                            display: "flex",
                            alignItems: "center",
                            gap: "6rem"
                        }}>
                            <span>Traffic Jam Alerts</span>
                            <span style={{
                                fontSize: "11rem",
                                fontWeight: 700,
                                backgroundColor: "rgba(255, 77, 77, 0.25)",
                                color: "#ff9999",
                                borderRadius: "10rem",
                                padding: "1rem 6rem"
                            }}>
                                {notifications.length}
                            </span>
                        </div>

                        {notifications.map((item, idx) => (
                            <div
                                key={`notif-${item.index}-${item.version}-${idx}`}
                                style={{
                                    display: "flex",
                                    flexDirection: "row",
                                    alignItems: "center",
                                    justifyContent: "space-between",
                                    padding: "8rem 10rem",
                                    borderRadius: "4rem",
                                    marginBottom: "4rem",
                                    backgroundColor: "rgba(255, 77, 77, 0.08)",
                                    border: "1px solid rgba(255, 77, 77, 0.22)"
                                }}
                            >
                                <div style={{ display: "flex", flexDirection: "row", alignItems: "center", gap: "10rem", flex: 1, overflow: "hidden" }}>
                                    <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minWidth: "24rem" }}>
                                        <TrafficJamAlertIcon />
                                    </div>
                                    <span style={{
                                        fontSize: "14rem",
                                        fontWeight: 600,
                                        color: "#ffffff",
                                        whiteSpace: "nowrap",
                                        overflow: "hidden",
                                        textOverflow: "ellipsis"
                                    }}>
                                        {cleanDisplayName(item.name) || "Traffic Bottleneck"}
                                    </span>
                                </div>

                                <div
                                    onClick={() => handleLocationClick(item.index, item.version)}
                                    style={{
                                        cursor: "pointer",
                                        padding: "5rem",
                                        marginLeft: "8rem",
                                        borderRadius: "4rem",
                                        backgroundColor: "rgba(255, 82, 82, 0.2)",
                                        border: "1px solid rgba(255, 82, 82, 0.4)",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center"
                                    }}
                                    title="Move camera to traffic jam"
                                >
                                    <LocationPinIcon />
                                </div>
                            </div>
                        ))}

                        {/* Section Divider */}
                        <div style={{
                            margin: "10rem 4rem 8rem 4rem",
                            borderBottom: "1px solid rgba(255, 255, 255, 0.1)"
                        }} />
                    </div>
                )}

                {/* 2. Blockers Section Header */}
                <div style={{
                    padding: "4rem 8rem 6rem 8rem",
                    fontSize: "12rem",
                    fontWeight: 600,
                    color: "rgba(255, 255, 255, 0.7)"
                }}>
                    Lead Blockers
                </div>

                {/* 3. Blockers List */}
                {blockers.length === 0 && notifications.length === 0 ? (
                    <div style={{ padding: "20rem", textAlign: "center", color: "rgba(255, 255, 255, 0.4)", fontSize: "13rem" }}>
                        No traffic jams or blockers detected.
                    </div>
                ) : blockers.length === 0 ? (
                    <div style={{ padding: "14rem", textAlign: "center", color: "rgba(255, 255, 255, 0.4)", fontSize: "13rem" }}>
                        No lead vehicle blockers detected.
                    </div>
                ) : (
                    blockers.map((item, idx) => (
                        <div
                            key={`blocker-${item.index}-${item.version}-${idx}`}
                            style={{
                                display: "flex",
                                flexDirection: "row",
                                alignItems: "center",
                                justifyContent: "space-between",
                                padding: "8rem 10rem",
                                borderRadius: "4rem",
                                marginBottom: "3rem",
                                backgroundColor: idx % 2 === 0 ? "rgba(255, 255, 255, 0.03)" : "transparent"
                            }}
                        >
                            <div style={{ display: "flex", flexDirection: "row", alignItems: "center", gap: "10rem", flex: 1, overflow: "hidden" }}>
                                <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minWidth: "24rem" }}>
                                    {renderVehicleIcon(item.type, item.name)}
                                </div>
                                <span style={{
                                    fontSize: "14rem",
                                    fontWeight: 600,
                                    color: "rgba(255, 255, 255, 0.95)",
                                    whiteSpace: "nowrap",
                                    overflow: "hidden",
                                    textOverflow: "ellipsis"
                                }}>
                                    {cleanDisplayName(item.name)}
                                </span>
                            </div>

                            <div style={{ display: "flex", flexDirection: "row", alignItems: "center", gap: "10rem", marginLeft: "8rem" }}>
                                <span style={{ fontSize: "14rem", fontWeight: 700, color: "white", minWidth: "22rem", textAlign: "right" }}>
                                    {item.waitingCount}
                                </span>
                                <div style={{ display: "flex", alignItems: "center", justifyContent: "center" }} title={item.reason === "boarding" ? "Boarding Passengers" : item.reason === "signal" ? "Traffic Signal Wait" : "Stopped / Maintenance / Unloading"}>
                                    {item.reason === "boarding" ? <BoardingIcon /> : item.reason === "signal" ? <SignalIcon /> : <StoppedIcon />}
                                </div>
                                <div
                                    onClick={() => handleLocationClick(item.index, item.version)}
                                    style={{
                                        cursor: "pointer",
                                        padding: "5rem",
                                        marginLeft: "8rem",
                                        borderRadius: "4rem",
                                        backgroundColor: "rgba(82, 184, 255, 0.15)",
                                        border: "1px solid rgba(82, 184, 255, 0.3)",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center"
                                    }}
                                    title="Move camera to location"
                                >
                                    <LocationPinIcon />
                                </div>
                            </div>
                        </div>
                    ))
                )}
            </div>
        </div>
    );
};
