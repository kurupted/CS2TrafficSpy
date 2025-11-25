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

// Local definitions to prevent "cs2/bindings" import errors
export interface Entity {
    index: number;
    version: number;
}

export interface Theme {
    entity: Entity;
    name: string;
    icon: string;
}