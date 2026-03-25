import React, { useState } from 'react';
import { bindValue, trigger } from "cs2/api";
import { useValue } from "cs2/api";

const showTransitPanel$ = bindValue<boolean>("TrafficSpy", "showTransitPanel", false);
const transitLinesData$ = bindValue<string>("TrafficSpy", "transitLinesData", "[]");

type TransitType = 'bus' | 'train' | 'subway' | 'tram';

interface TransitLine {
    id: number;
    type: TransitType;
    name: string;
    color: string;
    vehicles: number;
    passengers: number;
    length: string;
    usage: number;
}

export const TransitPanel = () => {
    const isVisible = useValue(showTransitPanel$);
    const rawData = useValue(transitLinesData$);
    const lines: TransitLine[] = JSON.parse(rawData);

    const [activeTab, setActiveTab] = useState<TransitType>('bus');
    const [showDepots, setShowDepots] = useState(true);
    const [showStations, setShowStations] = useState(true);
    const [activeLines, setActiveLines] = useState<Set<number>>(new Set([1, 2]));

    if (!isVisible) return null;

    const toggleLine = (id: number) => {
        const next = new Set(activeLines);
        if (next.has(id)) next.delete(id);
        else next.add(id);
        setActiveLines(next);
    };

    const currentLines = lines.filter(l => l.type === activeTab);

    return (
        <div style={{
            position: 'absolute',
            left: '60rem',
            top: '60rem',
            width: '320rem',
            backgroundColor: 'rgba(25, 30, 35, 0.95)',
            backdropFilter: 'blur(10px)',
            border: '1rem solid rgba(255, 255, 255, 0.1)',
            borderRadius: '8rem',
            color: 'white',
            display: 'flex',
            flexDirection: 'column',
            maxHeight: '80vh',
            fontFamily: 'sans-serif',
            boxShadow: '0 4rem 12rem rgba(0,0,0,0.5)',
            zIndex: 1000,
            pointerEvents: 'auto' // Crucial so clicks don't pass through to the game map
        }}>
            {/* Header */}
            <div style={{ padding: '15rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h2 style={{ margin: 0, fontSize: '16rem', fontWeight: 'bold' }}>Transit Overview</h2>
                <button
                    onClick={() => trigger("TrafficSpy", "toggleTransitPanel", false)}
                    style={{ background: 'none', border: 'none', color: '#aaa', cursor: 'pointer', fontSize: '16rem' }}>
                    ✕
                </button>
            </div>

            {/* Tabs */}
            <div style={{ display: 'flex', borderBottom: '1rem solid rgba(255,255,255,0.1)' }}>
                {['bus', 'train', 'subway', 'tram'].map((tab) => (
                    <button
                        key={tab}
                        onClick={() => setActiveTab(tab as TransitType)}
                        style={{
                            flex: 1,
                            padding: '10rem 0',
                            background: activeTab === tab ? 'rgba(255,255,255,0.1)' : 'transparent',
                            border: 'none',
                            color: activeTab === tab ? 'white' : '#888',
                            borderBottom: activeTab === tab ? '2rem solid #4287f5' : '2rem solid transparent',
                            cursor: 'pointer',
                            textTransform: 'capitalize',
                            fontSize: '14rem'
                        }}
                    >
                        {tab}
                    </button>
                ))}
            </div>

            {/* Infrastructure Toggles */}
            <div style={{ padding: '15rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', gap: '15rem' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '13rem', cursor: 'pointer' }}>
                    <input type="checkbox" checked={showDepots} onChange={e => setShowDepots(e.target.checked)} style={{ width: '16rem', height: '16rem' }}/>
                    Show Depots
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '13rem', cursor: 'pointer' }}>
                    <input type="checkbox" checked={showStations} onChange={e => setShowStations(e.target.checked)} style={{ width: '16rem', height: '16rem' }}/>
                    Show Stations
                </label>
            </div>

            {/* Line List */}
            <div style={{ padding: '10rem', overflowY: 'auto', flex: 1 }}>
                {currentLines.length === 0 ? (
                    <div style={{ padding: '20rem', textAlign: 'center', color: '#666', fontSize: '13rem' }}>No lines found.</div>
                ) : currentLines.map(line => (
                    <div key={line.id} style={{
                        display: 'flex',
                        alignItems: 'center',
                        padding: '10rem',
                        marginBottom: '8rem',
                        backgroundColor: 'rgba(255,255,255,0.05)',
                        borderRadius: '6rem',
                        borderLeft: `4rem solid ${line.color}`
                    }}>
                        <input
                            type="checkbox"
                            checked={activeLines.has(line.id)}
                            onChange={() => toggleLine(line.id)}
                            style={{ marginRight: '12rem', cursor: 'pointer', width: '16rem', height: '16rem' }}
                        />
                        <div style={{ flex: 1 }}>
                            {/* Top Row: Name */}
                            <div style={{ fontWeight: 'bold', fontSize: '14rem', marginBottom: '4rem' }}>
                                {line.name}
                            </div>
                            {/* Bottom Row: Stats */}
                            <div style={{ fontSize: '12rem', color: '#bbb', display: 'flex', gap: '10rem' }}>
                                <span>{line.vehicles} 🚌</span>
                                <span>{line.passengers} 👤</span>
                                <span>{line.length}</span>
                                <span>{line.usage}% full</span>
                            </div>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};