import { inject, Injectable, linkedSignal } from '@angular/core';
import { AuthenticationService } from '../core/auth/auth.service';

@Injectable({ providedIn: 'root' })
export class TranscriptPreferencesService {
  private readonly auth = inject(AuthenticationService);
  private readonly exclusions = linkedSignal<string[]>(() => this.readExclusions());
  readonly excludedSubjectCodes = this.exclusions.asReadonly();

  saveExcludedSubjects(codes: string[]): void {
    const normalized = this.normalize(codes);
    try {
      localStorage.setItem(this.storageKey(), JSON.stringify(normalized));
    } catch { /* Keep the preference for this session when browser storage is unavailable. */ }
    this.exclusions.set(normalized);
  }

  private storageKey(): string {
    return `transcripts.excludedSubjects.${this.auth.username() ?? ''}`;
  }

  private readExclusions(): string[] {
    const key = this.storageKey();
    try {
      const value: unknown = JSON.parse(localStorage.getItem(key) ?? '[]');
      return Array.isArray(value) ? this.normalize(value.filter((code) => typeof code === 'string')) : [];
    } catch { return []; }
  }

  private normalize(codes: string[]): string[] {
    return [...new Set(codes.map((code) => code.trim().toLowerCase()).filter(Boolean))].sort();
  }
}
