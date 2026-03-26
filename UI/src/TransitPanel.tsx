import React, { useState, useEffect } from 'react';
import { bindValue, trigger, useValue } from "cs2/api";

const showTransitPanel$ = bindValue<boolean>("TrafficSpy", "showTransitPanel", false);
const transitLinesData$ = bindValue<string>("TrafficSpy", "transitLinesData", "[]");

type TransitType = 'bus' | 'train' | 'subway' | 'tram' | 'ferry' | 'airplane' | 'ship' | 'cargo' | 'none';

interface TransitLine {
    id: number;
    type: TransitType;
    name: string;
    color: string;
    vehicles: number;
    passengers: number;
    length: string;
    usage: number;
    cargo: boolean; // ADDED: Cargo flag
    visible: boolean;
}

// --- Hardcoded #bbb SVGs ---
const VehicleIcon = () => (
    <svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb">
        <path d="M4 16c0 .88.39 1.67 1 2.22V20c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h8v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1.78c.61-.55 1-1.34 1-2.22V6c0-3.5-3.58-4-8-4s-8 .5-8 4v10zm3.5 1c-.83 0-1.5-.67-1.5-1.5S6.67 14 7.5 14s1.5.67 1.5 1.5S8.33 17 7.5 17zm9 0c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm1.5-6H6V6h12v5z"/>
    </svg>
);

const PassengerIcon = () => (
    <svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb">
        <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/>
    </svg>
);

const LengthIcon = () => (
    <svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb">
        <path d="M21 7H3c-1.1 0-2 .9-2 2v6c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V9c0-1.1-.9-2-2-2zm0 8H3V9h2v3h2V9h2v3h2V9h2v3h2V9h2v3h2V9h2v6z"/>
    </svg>
);

const UsageIcon = () => (
    <svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb">
        <path d="M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6h-6z"/>
    </svg>
);

const CustomCheckbox = ({ checked, onChange }: { checked: boolean, onChange: () => void }) => (
    <div onClick={onChange} style={{ width: '18rem', height: '18rem', border: '1rem solid rgba(255,255,255,0.3)', borderRadius: '4rem', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', backgroundColor: checked ? '#4287f5' : 'rgba(0,0,0,0.5)', flexShrink: 0 }}>
        {checked && <span style={{ color: 'white', fontSize: '14rem', lineHeight: '18rem' }}>✓</span>}
    </div>
);

export const TransitPanel = () => {
    const isVisible = useValue(showTransitPanel$);
    const rawData = useValue(transitLinesData$);

    const [activeTab, setActiveTab] = useState<TransitType>('bus');
    const [activeLines, setActiveLines] = useState<Set<number>>(new Set());

    let lines: TransitLine[] = [];
    try { if (rawData && rawData !== "[]") lines = JSON.parse(rawData); } catch (e) {}

    useEffect(() => {
        if (isVisible && lines.length > 0 && activeLines.size === 0) {
            setActiveLines(new Set(lines.filter(l => l.visible).map(l => l.id)));
        }
    }, [isVisible, lines]);

    if (!isVisible) return null;
    if (lines.length === 0) return (<div style={{ position: 'absolute', left: '60rem', top: '60rem', width: '320rem', backgroundColor: 'rgba(25, 30, 35, 0.95)', padding: '20rem', color: 'white' }}>Loading Transit Data...</div>);

    // CHANGED: Filter logic to handle the new Cargo tab independently
    const currentLines = lines.filter(l => {
        if (activeTab === 'cargo') return l.cargo;
        return !l.cargo && (l.type === activeTab || (activeTab === 'bus' && l.type === 'none'));
    });

    const allVisible = currentLines.length > 0 && currentLines.every(l => activeLines.has(l.id));

    const toggleLine = (id: number) => {
        const next = new Set(activeLines);
        let willShow = false;
        if (next.has(id)) next.delete(id); else { next.add(id); willShow = true; }
        setActiveLines(next);
        trigger("TrafficSpy", "setLineVisible", id, willShow);
    };

    const toggleAll = () => {
        const next = new Set(activeLines);
        const targetState = !allVisible;
        currentLines.forEach(l => {
            if (targetState) next.add(l.id); else next.delete(l.id);
            trigger("TrafficSpy", "setLineVisible", l.id, targetState);
        });
        setActiveLines(next);
    };

    return (
        <div style={{ position: 'absolute', top: '235rem', left: '10rem', width: '450rem', maxHeight: '740rem', backgroundColor: 'var(--panelColorNormal)', borderRadius: '4rem', padding: '12rem', color: 'white', pointerEvents: 'auto', boxShadow: '0 4px 8px rgba(0,0,0,0.3)', display: 'flex', flexDirection: 'column' }}>
            <div style={{ padding: '15rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h2 style={{ margin: 0, fontSize: '16rem', fontWeight: 'bold' }}>Transit Overview</h2>
                <button onClick={() => trigger("TrafficSpy", "toggleTransitCustom", false)} style={{ background: 'none', border: 'none', color: '#aaa', cursor: 'pointer', fontSize: '16rem' }}>✕</button>
            </div>

            <div style={{ display: 'flex', borderBottom: '1rem solid rgba(255,255,255,0.1)' }}>
                {/* ADDED: Cargo tab */}
                {['bus', 'train', 'subway', 'tram', 'ferry', 'cargo'].map((tab) => (
                    <button key={tab} onClick={() => setActiveTab(tab as TransitType)} style={{ flex: 1, padding: '10rem 0', cursor: 'pointer', textTransform: 'capitalize', fontSize: '13rem', background: activeTab === tab ? 'rgba(255,255,255,0.1)' : 'transparent', border: 'none', color: activeTab === tab ? 'white' : '#888', borderBottom: activeTab === tab ? '2rem solid #4287f5' : '2rem solid transparent' }}>
                        {tab}
                    </button>
                ))}
            </div>

            <div style={{ padding: '10rem 15rem', backgroundColor: 'rgba(0,0,0,0.2)', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: '13rem', fontWeight: 'bold', color: '#aaa' }}>{activeTab.toUpperCase()} LINES</span>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '13rem', cursor: 'pointer' }}>Toggle All <CustomCheckbox checked={allVisible} onChange={toggleAll} /></label>
            </div>

            <div style={{ padding: '10rem', overflowY: 'auto', flex: 1 }}>
                {currentLines.length === 0 ? (
                    <div style={{ padding: '20rem', textAlign: 'center', color: '#666', fontSize: '13rem' }}>No lines found.</div>
                ) : currentLines.map(line => (
                    <div key={line.id} style={{ display: 'flex', alignItems: 'center', padding: '10rem', marginBottom: '8rem', backgroundColor: 'rgba(255,255,255,0.05)', borderRadius: '6rem', borderLeft: `4rem solid ${line.color}` }}>
                        <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
                            <div style={{ fontWeight: 'bold', fontSize: '14rem', marginBottom: '8rem', whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
                                {line.name}
                            </div>
                            {/* CHANGED: Increased the gap drastically to separate the stats visually */}
                            <div style={{ fontSize: '12rem', color: '#bbb', display: 'flex', flexWrap: 'wrap', gap: '16rem 24rem' }}>
                                <span style={{ display: 'flex', alignItems: 'center', gap: '6rem' }}><VehicleIcon /> {line.vehicles}</span>
                                <span style={{ display: 'flex', alignItems: 'center', gap: '6rem' }}><PassengerIcon /> {line.passengers}</span>
                                <span style={{ display: 'flex', alignItems: 'center', gap: '6rem' }}><LengthIcon /> {line.length}</span>
                                <span style={{ display: 'flex', alignItems: 'center', gap: '6rem' }}><UsageIcon /> {line.usage}%</span>
                            </div>
                        </div>
                        <div style={{ marginLeft: '15rem', flexShrink: 0 }}>
                            <CustomCheckbox checked={activeLines.has(line.id)} onChange={() => toggleLine(line.id)} />
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};