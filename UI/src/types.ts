export interface SegmentActivity {
    workers: number;
    students: number;
    shoppers: number;
    goingHome: number;
    healthcare: number;
    cargo: number;
    services: number;
    publicTransport: number;
    other: number;
}

// Add these here so we don't have to import them from cs2/bindings
export interface Entity {
    index: number;
    version: number;
}

export interface Theme {
    entity: Entity;
    name: string;
    icon: string;
}