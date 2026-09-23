import { Component, Input } from '@angular/core';
import { CustomFieldType } from './import-module-fields.service';

@Component({
  selector: 'app-field-type-icon',
  template: `
    <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      @switch (type) {
        @case ('text') { <path d="M4 7h16M4 12h10" /> }
        @case ('textarea') { <path d="M4 6h16M4 10h16M4 14h12M4 18h8" /> }
        @case ('number') { <path d="M10 3 8 21M16 3l-2 18M4 9h16M3 15h16" /> }
        @case ('date') { <rect x="4" y="5" width="16" height="15" rx="1" /><path d="M8 3v4M16 3v4M4 9h16" /><circle cx="15" cy="14" r="1.5" fill="currentColor" stroke="none" /> }
        @case ('select') { <circle cx="12" cy="12" r="9" /><path d="m8.5 10 3.5 3.5 3.5-3.5" /> }
        @case ('checkbox') { <rect x="4" y="4" width="16" height="16" rx="1" /><path d="m8 12 3 3 6-7" /> }
      }
    </svg>
  `,
})
export class FieldTypeIconComponent {
  @Input({ required: true }) type!: CustomFieldType;
}
