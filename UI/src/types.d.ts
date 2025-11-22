// 1. Handle CSS Modules and Images
declare module "*.module.scss" {
    const classes: { [key: string]: string };
    export default classes;
}

declare module "*.svg" {
    const content: string;
    export default content;
}

// 2. Define Game API Types (Simplified)
declare module "cs2/api" {
    export interface ValueBinding<T> {
        value: T;
        subscribe(callback: (value: T) => void): () => void;
    }
    export function bindValue<T>(group: string, name: string, fallback?: T): ValueBinding<T>;
    export function trigger(group: string, name: string, value?: any): void;
    export function useValue<T>(binding: ValueBinding<T>): T;
}

declare module "cs2/ui" {
    import { ComponentType, ReactNode } from "react";
    
    export interface FloatingButtonProps {
        src: string;
        selected?: boolean;
        onClick?: () => void;
        tooltip?: string;
    }
    export const FloatingButton: ComponentType<FloatingButtonProps>;

    export interface TooltipProps {
        tooltip: string | ReactNode;
        children: ReactNode;
    }
    export const Tooltip: ComponentType<TooltipProps>;
}

declare module "cs2/modding" {
    export interface ModuleRegistry {
        append(id: string, component: any): void;
        extend(id: string, extensionId: string, component: any): void;
    }
    export type ModRegistrar = (registry: ModuleRegistry) => void;
}

declare module "cs2/l10n" {
    export function useLocalization(): { translate: (key: string, fallback?: string) => string };
}