import React, { useState, useEffect } from 'react';
// Added 'trigger' and 'bindValue' to the imports
import { bindValue, trigger, useValue } from "cs2/api";

// 1. Define the binding for Zone Colors
const showTransitPanel$ = bindValue<boolean>("TrafficSpy", "showTransitPanel", false);
const transitLinesData$ = bindValue<string>("TrafficSpy", "transitLinesData", "[]");
const useZoneColors$ = bindValue<boolean>("TrafficSpy", "useZoneColors", false);

type TransitType = 'bus' | 'train' | 'subway' | 'tram' | 'airplane' | 'ship' | 'none';

interface TransitLine {
    id: number;
    type: TransitType;
    name: string;
    color: string;
    vehicles: number;
    passengers: number;
    length: string;
    usage: number;
    visible: boolean;
}

// CustomCheckbox remains the same, accepting () => void
const CustomCheckbox = ({ checked, onChange }: { checked: boolean, onChange: () => void }) => (
    <div
        onClick={onChange}
        style={{
            width: '18rem', height: '18rem', border: '1rem solid rgba(255,255,255,0.3)',
            borderRadius: '4rem', display: 'flex', alignItems: 'center', justifyContent: 'center',
            cursor: 'pointer', backgroundColor: checked ? '#4287f5' : 'rgba(0,0,0,0.5)', flexShrink: 0
        }}
    >
        {checked && <span style={{ color: 'white', fontSize: '14rem', lineHeight: '18rem' }}>✓</span>}
    </div>
);

export const TransitPanel = () => {
    const isVisible = useValue(showTransitPanel$);
    const rawData = useValue(transitLinesData$);

    let lines: TransitLine[] = [];
    try {
        if (rawData && rawData !== "[]") {
            lines = JSON.parse(rawData);
        }
    } catch (e) {
        console.error("TrafficSpy UI: Failed to parse transit data", e);
    }

    // Safety: If data isn't ready, show a loading state instead of crashing
    if (!isVisible) return null;
    if (lines.length === 0) {
        return (
            <div>
                <div>Loading Transit Data...</div>
            </div>
        );
    }
    
    const useZoneColors = useValue(useZoneColors$);
    const [activeTab, setActiveTab] = useState<TransitType>('bus');
    const [showDepots, setShowDepots] = useState(true);
    const [showStations, setShowStations] = useState(true);
    const [activeLines, setActiveLines] = useState<Set<number>>(new Set());

    useEffect(() => {
        if (isVisible && lines.length > 0 && activeLines.size === 0) {
            const visibleIds = lines.filter(l => l.visible).map(l => l.id);
            setActiveLines(new Set(visibleIds));
        }
    }, [isVisible, lines]);

    if (!isVisible) return null;

    const currentLines = lines.filter(l => l.type === activeTab || (activeTab === 'bus' && l.type === 'none'));
    const allVisible = currentLines.length > 0 && currentLines.every(l => activeLines.has(l.id));

    const toggleLine = (id: number) => {
        const next = new Set(activeLines);
        let willShow = false;
        if (next.has(id)) {
            next.delete(id);
        } else {
            next.add(id);
            willShow = true;
        }
        setActiveLines(next);
        trigger("TrafficSpy", "setLineVisible", id, willShow);
    };

    const toggleAll = () => {
        const next = new Set(activeLines);
        const targetState = !allVisible;
        currentLines.forEach(l => {
            if (targetState) next.add(l.id);
            else next.delete(l.id);
            trigger("TrafficSpy", "setLineVisible", l.id, targetState);
        });
        setActiveLines(next);
    };

    return (
        <div style={{
            position: 'absolute', left: '60rem', top: '60rem', width: '320rem',
            backgroundColor: 'rgba(25, 30, 35, 0.95)', backdropFilter: 'blur(10px)',
            border: '1rem solid rgba(255, 255, 255, 0.1)', borderRadius: '8rem',
            color: 'white', display: 'flex', flexDirection: 'column',
            maxHeight: '80vh', fontFamily: 'sans-serif',
            boxShadow: '0 4rem 12rem rgba(0,0,0,0.5)', zIndex: 1000, pointerEvents: 'auto'
        }}>
            <div style={{ padding: '15rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h2 style={{ margin: 0, fontSize: '16rem', fontWeight: 'bold' }}>Transit Overview</h2>

                {/* 3. Updated Zone Colors Checkbox Fix */}
                <div style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '12rem' }}>
                    <CustomCheckbox
                        checked={useZoneColors}
                        onChange={() => trigger('TrafficSpy', 'setUseZoneColors', !useZoneColors)}
                    />
                    <span>Zones</span>
                </div>

                <button
                    onClick={() => trigger("TrafficSpy", "toggleTransitCustom", false)}
                    style={{ background: 'none', border: 'none', color: '#aaa', cursor: 'pointer', fontSize: '16rem' }}>✕</button>
            </div>

            <div style={{ display: 'flex', borderBottom: '1rem solid rgba(255,255,255,0.1)' }}>
                {['bus', 'train', 'subway', 'tram'].map((tab) => (
                    <button
                        key={tab} onClick={() => setActiveTab(tab as TransitType)}
                        style={{
                            flex: 1, padding: '10rem 0', cursor: 'pointer', textTransform: 'capitalize', fontSize: '14rem',
                            background: activeTab === tab ? 'rgba(255,255,255,0.1)' : 'transparent', border: 'none',
                            color: activeTab === tab ? 'white' : '#888',
                            borderBottom: activeTab === tab ? '2rem solid #4287f5' : '2rem solid transparent',
                        }}
                    >
                        {tab}
                    </button>
                ))}
            </div>

            <div style={{ padding: '15rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', gap: '20rem' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '13rem', cursor: 'pointer' }}>
                    <CustomCheckbox checked={showDepots} onChange={() => setShowDepots(!showDepots)} /> Show Depots
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '13rem', cursor: 'pointer' }}>
                    <CustomCheckbox checked={showStations} onChange={() => setShowStations(!showStations)} /> Show Stations
                </label>
            </div>

            {/* Toggle All Row */}
            <div style={{ padding: '10rem 15rem', backgroundColor: 'rgba(0,0,0,0.2)', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: '13rem', fontWeight: 'bold', color: '#aaa' }}>{activeTab.toUpperCase()} LINES</span>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '13rem', cursor: 'pointer' }}>
                    Toggle All
                    <CustomCheckbox checked={allVisible} onChange={toggleAll} />
                </label>
            </div>

            <div style={{ padding: '10rem', overflowY: 'auto', flex: 1 }}>
                {currentLines.length === 0 ? (
                    <div style={{ padding: '20rem', textAlign: 'center', color: '#666', fontSize: '13rem' }}>No lines found.</div>
                ) : currentLines.map(line => (
                    <div key={line.id} style={{
                        display: 'flex', alignItems: 'center', padding: '10rem', marginBottom: '8rem',
                        backgroundColor: 'rgba(255,255,255,0.05)', borderRadius: '6rem',
                        borderLeft: `4rem solid ${line.color}`
                    }}>
                        <div style={{ flex: 1, overflow: 'hidden' }}>
                            <div style={{ fontWeight: 'bold', fontSize: '14rem', marginBottom: '4rem', whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
                                {line.name}
                            </div>
                            <div style={{ fontSize: '12rem', color: '#bbb', display: 'flex', gap: '10rem' }}>
                                <span>{line.vehicles} 🚌</span>
                                <span>{line.passengers} 👤</span>
                                <span>{line.length}</span>
                                <span>{line.usage}%</span>
                            </div>
                        </div>
                        <div style={{ marginLeft: '10rem' }}>
                            {/* Individual Checkbox referencing local state */}
                            <CustomCheckbox checked={activeLines.has(line.id)} onChange={() => toggleLine(line.id)} />
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};