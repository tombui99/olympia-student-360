import { computed, Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ApiLoadingService {
  private readonly pendingRequests = signal(0);

  readonly loading = computed(() => this.pendingRequests() > 0);

  begin(): void {
    this.pendingRequests.update((count) => count + 1);
  }

  end(): void {
    this.pendingRequests.update((count) => Math.max(0, count - 1));
  }
}
