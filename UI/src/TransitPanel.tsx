import React, { useState, useEffect } from 'react';
import { bindValue, trigger, useValue } from "cs2/api";

const showTransitPanel$ = bindValue<boolean>("TrafficSpy", "showTransitPanel", false);
const transitLinesData$ = bindValue<string>("TrafficSpy", "transitLinesData", "[]");
const showStopsAndStations$ = bindValue<boolean>("TrafficSpy", "showStopsAndStations", true);
const showInfoviewBackground$ = bindValue<boolean>("TrafficSpy", "showInfoviewBackground", true);

type TransitType = 'bus' | 'train' | 'subway' | 'tram' | 'ferry' | 'airplane' | 'ship' | 'cargo' | 'none';
type SortField = 'name' | 'usage' | 'vehicles' | 'length' | 'passengers';

interface TransitLine {
    id: number;
    type: TransitType;
    name: string;
    color: string;
    vehicles: number;
    passengers: number;
    length: string;
    lengthRaw: number; // For mathematical sorting
    usage: number;
    cargo: boolean;
    visible: boolean;
}

const VehicleIcon = () => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M4 16c0 .88.39 1.67 1 2.22V20c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h8v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1.78c.61-.55 1-1.34 1-2.22V6c0-3.5-3.58-4-8-4s-8 .5-8 4v10zm3.5 1c-.83 0-1.5-.67-1.5-1.5S6.67 14 7.5 14s1.5.67 1.5 1.5S8.33 17 7.5 17zm9 0c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm1.5-6H6V6h12v5z"/></svg>);
const PassengerIcon = () => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>);
const LengthIcon = () => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M21 7H3c-1.1 0-2 .9-2 2v6c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V9c0-1.1-.9-2-2-2zm0 8H3V9h2v3h2V9h2v3h2V9h2v3h2V9h2v6z"/></svg>);
const UsageIcon = () => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6h-6z"/></svg>);

const CustomCheckbox = ({ checked, onChange }: { checked: boolean, onChange: () => void }) => (
    <div onClick={onChange} style={{ width: '18rem', height: '18rem', border: '1rem solid rgba(255,255,255,0.3)', borderRadius: '4rem', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', backgroundColor: checked ? '#4287f5' : 'rgba(0,0,0,0.5)', flexShrink: 0 }}>
        {checked && <span style={{ color: 'white', fontSize: '14rem', lineHeight: '18rem' }}>✓</span>}
    </div>
);

export const TransitPanel = () => {
    const isVisible = useValue(showTransitPanel$);
    const rawData = useValue(transitLinesData$);
    const showStopsAndStations = useValue(showStopsAndStations$);
    const showInfoviewBackground = useValue(showInfoviewBackground$);

    const [activeTab, setActiveTab] = useState<TransitType>('bus');
    const [activeLines, setActiveLines] = useState<Set<number>>(new Set());

    // Sorting States
    const [sortField, setSortField] = useState<SortField>('name');
    const [sortDesc, setSortDesc] = useState<boolean>(false);

    let lines: TransitLine[] = [];
    try { if (rawData && rawData !== "[]") lines = JSON.parse(rawData); } catch (e) {}

    useEffect(() => {
        if (isVisible && lines.length > 0 && activeLines.size === 0) {
            setActiveLines(new Set(lines.filter(l => l.visible).map(l => l.id)));
        }
    }, [isVisible, lines]);

    if (!isVisible) return null;
    if (lines.length === 0) return (<div style={{ position: 'absolute', left: '60rem', top: '60rem', width: '320rem', backgroundColor: 'rgba(25, 30, 35, 0.95)', padding: '20rem', color: 'white' }}>Loading Transit Data...</div>);

    const currentLines = lines.filter(l => {
        if (activeTab === 'cargo') return l.cargo;
        return !l.cargo && (l.type === activeTab || (activeTab === 'bus' && l.type === 'none'));
    });

    // Sorting Logic
    const sortedLines = [...currentLines].sort((a, b) => {
        let valA: any = a[sortField];
        let valB: any = b[sortField];

        // Ensure length uses numerical distance, and name ignores casing
        if (sortField === 'length') { valA = a.lengthRaw; valB = b.lengthRaw; }
        else if (sortField === 'name') { valA = valA.toLowerCase(); valB = valB.toLowerCase(); }

        if (valA < valB) return sortDesc ? 1 : -1;
        if (valA > valB) return sortDesc ? -1 : 1;
        return 0;
    });

    const allVisibleInTab = sortedLines.length > 0 && sortedLines.every(l => activeLines.has(l.id));

    const toggleLine = (id: number) => {
        const next = new Set(activeLines);
        let willShow = false;
        if (next.has(id)) next.delete(id); else { next.add(id); willShow = true; }
        setActiveLines(next);
        trigger("TrafficSpy", "setLineVisible", id, willShow);
    };

    const toggleTabAll = () => {
        const next = new Set(activeLines);
        const targetState = !allVisibleInTab;
        sortedLines.forEach(l => {
            if (targetState) next.add(l.id); else next.delete(l.id);
            trigger("TrafficSpy", "setLineVisible", l.id, targetState);
        });
        setActiveLines(next);
    };

    const toggleMasterAll = () => {
        const isAnythingOff = lines.some(l => !activeLines.has(l.id)) || !showStopsAndStations;
        const targetState = isAnythingOff;

        const next = new Set<number>();
        lines.forEach(l => {
            if (targetState) next.add(l.id);
        });
        setActiveLines(next);

        trigger("TrafficSpy", "setAllLinesVisible", targetState);
        trigger("TrafficSpy", "setShowStopsAndStations", targetState);
    };

    return (
        <div style={{ position: 'absolute', top: '50rem', left: '10rem', width: '450rem', maxHeight: '800rem', backgroundColor: 'var(--panelColorNormal)', borderRadius: '4rem', padding: '12rem', color: 'white', pointerEvents: 'auto', boxShadow: '0 4px 8px rgba(0,0,0,0.3)', display: 'flex', flexDirection: 'column' }}>

            <div style={{ padding: '15rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h2 style={{ margin: 0, fontSize: '16rem', fontWeight: 'bold' }}>Transit Overview</h2>
                <div style={{ display: 'flex', alignItems: 'center', gap: '15rem' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '6rem', fontSize: '12rem', cursor: 'pointer', color: '#ccc' }}>
                        <CustomCheckbox checked={showInfoviewBackground} onChange={() => trigger("TrafficSpy", "setShowInfoviewBackground", !showInfoviewBackground)} />
                        Color Map
                    </label>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '6rem', fontSize: '12rem', cursor: 'pointer', color: '#ccc' }}>
                        <CustomCheckbox checked={showStopsAndStations} onChange={() => trigger("TrafficSpy", "setShowStopsAndStations", !showStopsAndStations)} />
                        Nodes
                    </label>
                    <button onClick={toggleMasterAll} style={{ backgroundColor: 'rgba(255,255,255,0.1)', border: '1rem solid rgba(255,255,255,0.2)', color: 'white', padding: '4rem 8rem', borderRadius: '4rem', cursor: 'pointer', fontSize: '11rem', textTransform: 'uppercase' }}>
                        Toggle All
                    </button>
                    {/* SVG Close Button */}
                    <button onClick={() => trigger("TrafficSpy", "toggleTransitCustom", false)} style={{ background: 'none', border: 'none', cursor: 'pointer', marginLeft: '5rem', padding: '4rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                        <svg viewBox="0 0 24 24" style={{ width: '18rem', height: '18rem' }} fill="none" stroke="#aaa" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            <line x1="18" y1="6" x2="6" y2="18"></line>
                            <line x1="6" y1="6" x2="18" y2="18"></line>
                        </svg>
                    </button>
                </div>
            </div>

            <div style={{ display: 'flex', borderBottom: '1rem solid rgba(255,255,255,0.1)' }}>
                {['bus', 'train', 'subway', 'tram', 'ferry', 'cargo'].map((tab) => (
                    <button key={tab} onClick={() => setActiveTab(tab as TransitType)} style={{ flex: 1, padding: '10rem 0', cursor: 'pointer', fontSize: '13rem', background: activeTab === tab ? 'rgba(255,255,255,0.1)' : 'transparent', border: 'none', color: activeTab === tab ? 'white' : '#888', borderBottom: activeTab === tab ? '2rem solid #4287f5' : '2rem solid transparent' }}>
                        {tab.charAt(0).toUpperCase() + tab.slice(1)}
                    </button>
                ))}
            </div>

            <div style={{ padding: '10rem 15rem', backgroundColor: 'rgba(0,0,0,0.2)', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '15rem' }}>
                    <span style={{ fontSize: '13rem', fontWeight: 'bold', color: '#aaa' }}>{activeTab.toUpperCase()} LINES</span>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '5rem', fontSize: '12rem', color: '#888' }}>
                        Sort:
                        <select value={sortField} onChange={(e) => setSortField(e.target.value as SortField)} style={{ background: 'transparent', color: '#fff', border: 'none', outline: 'none', cursor: 'pointer', padding: '0 2rem' }}>
                            <option value="name" style={{ color: 'black' }}>Name</option>
                            <option value="usage" style={{ color: 'black' }}>Usage %</option>
                            <option value="vehicles" style={{ color: 'black' }}>Vehicles</option>
                            <option value="passengers" style={{ color: 'black' }}>Passengers</option>
                            <option value="length" style={{ color: 'black' }}>Distance</option>
                        </select>
                        <button onClick={() => setSortDesc(!sortDesc)} style={{ background: 'none', border: 'none', color: '#aaa', cursor: 'pointer', padding: '0 5rem', fontSize: '14rem' }}>
                            {sortDesc ? '↓' : '↑'}
                        </button>
                    </div>
                </div>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '13rem', cursor: 'pointer' }}>Toggle Tab <CustomCheckbox checked={allVisibleInTab} onChange={toggleTabAll} /></label>
            </div>

            <div style={{ padding: '10rem', overflowY: 'auto', flex: 1 }}>
                {sortedLines.length === 0 ? (
                    <div style={{ padding: '20rem', textAlign: 'center', color: '#666', fontSize: '13rem' }}>No lines found.</div>
                ) : currentLines.map(line => (
                    <div key={line.id} style={{ display: 'flex', alignItems: 'center', padding: '10rem', marginBottom: '8rem', backgroundColor: 'rgba(255,255,255,0.05)', borderRadius: '6rem', borderLeft: `4rem solid ${line.color}`, overflowY:'scroll' }}>
                        <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
                            <div style={{ fontWeight: 'bold', fontSize: '16rem', marginBottom: '8rem', whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
                                {line.name}
                            </div>
                            <div style={{ fontSize: '14rem', color: '#bbb', display: 'flex', flexWrap: 'wrap', rowGap: '16rem', columnGap: '24rem' }}>
                                <span style={{ display: 'flex', alignItems: 'center', paddingRight: '10rem' }}><VehicleIcon /> {line.vehicles}</span>
                                <span style={{ display: 'flex', alignItems: 'center', paddingRight: '10rem' }}><PassengerIcon /> {line.passengers}</span>
                                <span style={{ display: 'flex', alignItems: 'center', paddingRight: '10rem' }}><LengthIcon /> {line.length}</span>
                                <span style={{ display: 'flex', alignItems: 'center', paddingRight: '10rem' }}><UsageIcon /> {line.usage}%</span>
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