import { Component, Input } from '@angular/core';
import { FormQuestionType } from './custom-forms.service';

@Component({
  selector: 'app-question-type-icon',
  template: `
    <svg class="h-6 w-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      @switch (type) {
        @case ('shortText') { <path d="M4 7h16M4 12h10" /> }
        @case ('paragraph') { <path d="M4 6h16M4 10h16M4 14h12M4 18h8" /> }
        @case ('multipleChoice') { <circle cx="12" cy="12" r="8" /><circle cx="12" cy="12" r="4" fill="currentColor" stroke="none" /> }
        @case ('checkboxes') { <rect x="4" y="4" width="16" height="16" rx="1" /><path d="m8 12 3 3 6-7" /> }
        @case ('dropdown') { <circle cx="12" cy="12" r="9" /><path d="m8.5 10 3.5 3.5 3.5-3.5" /> }
        @case ('linearScale') { <circle cx="5" cy="12" r="1.5" fill="currentColor" /><circle cx="12" cy="12" r="1.5" fill="currentColor" /><circle cx="19" cy="12" r="1.5" fill="currentColor" /><path d="M6.5 12h4M13.5 12h4" /> }
        @case ('rating') { <path d="m12 3 2.8 5.7 6.2.9-4.5 4.4 1.1 6.2-5.6-2.9-5.6 2.9 1.1-6.2L3 9.6l6.2-.9Z" /> }
        @case ('multipleChoiceGrid') {
          <circle cx="6" cy="6" r="1.8" fill="currentColor" /><circle cx="12" cy="6" r="1.8" fill="currentColor" /><circle cx="18" cy="6" r="1.8" fill="currentColor" />
          <circle cx="6" cy="12" r="1.8" fill="currentColor" /><circle cx="12" cy="12" r="1.8" fill="currentColor" /><circle cx="18" cy="12" r="1.8" fill="currentColor" />
          <circle cx="6" cy="18" r="1.8" fill="currentColor" /><circle cx="12" cy="18" r="1.8" fill="currentColor" /><circle cx="18" cy="18" r="1.8" fill="currentColor" />
        }
        @case ('checkboxGrid') {
          <rect x="4" y="4" width="4" height="4" fill="currentColor" stroke="none" /><rect x="10" y="4" width="4" height="4" fill="currentColor" stroke="none" /><rect x="16" y="4" width="4" height="4" fill="currentColor" stroke="none" />
          <rect x="4" y="10" width="4" height="4" fill="currentColor" stroke="none" /><rect x="10" y="10" width="4" height="4" fill="currentColor" stroke="none" /><rect x="16" y="10" width="4" height="4" fill="currentColor" stroke="none" />
          <rect x="4" y="16" width="4" height="4" fill="currentColor" stroke="none" /><rect x="10" y="16" width="4" height="4" fill="currentColor" stroke="none" /><rect x="16" y="16" width="4" height="4" fill="currentColor" stroke="none" />
        }
        @case ('date') { <rect x="4" y="5" width="16" height="15" rx="1" /><path d="M8 3v4M16 3v4M4 9h16" /><circle cx="15" cy="14" r="1.5" fill="currentColor" stroke="none" /> }
        @case ('time') { <circle cx="12" cy="12" r="9" /><path d="M12 7v5l3.5 2" /> }
      }
    </svg>
  `,
})
export class QuestionTypeIconComponent {
  @Input({ required: true }) type!: FormQuestionType;
}
