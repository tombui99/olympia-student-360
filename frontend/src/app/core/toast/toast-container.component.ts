import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ToastService, ToastType } from './toast.service';

@Component({
  selector: 'app-toast-container',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pointer-events-none fixed inset-x-4 top-4 z-[200] flex flex-col items-end gap-3 sm:left-auto sm:w-[min(24rem,calc(100vw-2rem))]" aria-live="polite" aria-atomic="false">
      @for (toast of toastService.toasts(); track toast.id) {
        <div
          class="pointer-events-auto flex w-full items-start gap-3 rounded-lg border bg-white px-4 py-3 shadow-[0_12px_32px_rgba(36,24,47,.18)]"
          [class.border-emerald-200]="toast.type === 'success'"
          [class.border-red-200]="toast.type === 'error'"
          [class.border-blue-200]="toast.type === 'info'"
          [class.border-amber-200]="toast.type === 'warning'"
          [attr.role]="toast.type === 'error' ? 'alert' : 'status'">
          <span class="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-full text-sm font-bold text-white" [class]="iconClass(toast.type)">{{ icon(toast.type) }}</span>
          <p class="min-w-0 flex-1 text-sm font-medium leading-6 text-[#403548]">{{ toast.message }}</p>
          <button type="button" class="mt-0.5 shrink-0 rounded p-0.5 text-lg leading-none text-[#817688] hover:bg-[#f3eff5] hover:text-[#32124f]" aria-label="Dismiss notification" (click)="toastService.dismiss(toast.id)">×</button>
        </div>
      }
    </div>
  `,
})
export class ToastContainerComponent {
  constructor(readonly toastService: ToastService) {}

  icon(type: ToastType) {
    return type === 'success' ? '✓' : type === 'error' ? '!' : type === 'warning' ? '!' : 'i';
  }

  iconClass(type: ToastType) {
    return type === 'success' ? 'bg-emerald-600' : type === 'error' ? 'bg-red-600' : type === 'warning' ? 'bg-amber-500' : 'bg-blue-600';
  }
}
