import { Injectable, signal } from '@angular/core';

export type AdminToastType = 'success' | 'error';

export interface AdminToastMessage {
    id: number;
    type: AdminToastType;
    message: string;
}

@Injectable({ providedIn: 'root' })
export class AdminToastService {
    private nextId = 1;
    private readonly toastsState = signal<AdminToastMessage[]>([]);

    readonly toasts = this.toastsState.asReadonly();

    showSuccess(message: string, timeoutMs = 3500): void {
        this.pushToast('success', message, timeoutMs);
    }

    showError(message: string, timeoutMs = 4500): void {
        this.pushToast('error', message, timeoutMs);
    }

    dismiss(id: number): void {
        this.toastsState.update((items) => items.filter((toast) => toast.id !== id));
    }

    private pushToast(type: AdminToastType, message: string, timeoutMs: number): void {
        const toast: AdminToastMessage = {
            id: this.nextId++,
            type,
            message
        };

        this.toastsState.update((items) => [...items, toast]);

        window.setTimeout(() => {
            this.dismiss(toast.id);
        }, timeoutMs);
    }
}
