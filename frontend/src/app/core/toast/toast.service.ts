import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

export interface ToastMessage {
  id: number;
  message: string;
  type: ToastType;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<ToastMessage[]>([]);
  private nextId = 1;
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();

  show(message: string, type: ToastType = 'info', duration = 4000): number {
    const id = this.nextId++;
    this.toasts.update(items => [...items, { id, message, type }]);
    if (duration > 0) this.timers.set(id, setTimeout(() => this.dismiss(id), duration));
    return id;
  }

  success(message: string, duration?: number) { return this.show(message, 'success', duration); }
  error(message: string, duration?: number) { return this.show(message, 'error', duration); }
  info(message: string, duration?: number) { return this.show(message, 'info', duration); }
  warning(message: string, duration?: number) { return this.show(message, 'warning', duration); }

  dismiss(id: number) {
    const timer = this.timers.get(id);
    if (timer) clearTimeout(timer);
    this.timers.delete(id);
    this.toasts.update(items => items.filter(item => item.id !== id));
  }
}
