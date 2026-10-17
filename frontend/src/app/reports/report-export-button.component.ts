import { Component, EventEmitter, Input, Output } from '@angular/core';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-report-export-button',
  template: `
    <button
      class="no-print inline-flex items-center gap-2 rounded-md border border-[#d8d0df] bg-white px-3.5 py-2 text-sm font-semibold text-[#32124f] transition hover:border-[#b69ac6] hover:bg-[#f4eef7] disabled:cursor-not-allowed disabled:opacity-50"
      type="button"
      [disabled]="disabled || exporting"
      (click)="exportRequested.emit()"
    >
      @if (exporting) {
        <span class="h-4 w-4 animate-spin rounded-full border-2 border-[#d7cddd] border-t-[#6d3a91]"></span>
      } @else {
        <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M12 3v12"/><path d="m7 10 5 5 5-5"/><path d="M5 21h14"/>
        </svg>
      }
      {{ languageService.t(exporting ? 'reports.exporting' : 'reports.exportExcel') }}
    </button>
  `,
})
export class ReportExportButtonComponent {
  @Input() exporting = false;
  @Input() disabled = false;
  @Output() readonly exportRequested = new EventEmitter<void>();

  constructor(readonly languageService: LanguageService) {}
}
