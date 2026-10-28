import { Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../core/language/language.service';
import { activityMonths, activityWeeks, ActivityPeriodSelection, normalizeActivityPeriod } from './activity-period';

@Component({
  selector: 'app-activity-period-filter',
  imports: [FormsModule],
  template: `
    <div class="flex flex-wrap items-end gap-3">
      <label class="grid w-full gap-1.5 sm:w-[180px]">
        <span class="text-[10px] font-bold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('reports.learningReview.month') }}</span>
        <select class="w-full min-w-0 rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3] disabled:opacity-60"
          [ngModel]="month" [ngModelOptions]="{standalone: true}" (ngModelChange)="selectMonth($event)" [disabled]="disabled">
          <option [ngValue]="null">{{ t('reports.learningReview.allMonths') }}</option>
          @for (option of months(); track option.value) { <option [ngValue]="option.value">{{ option.label }}</option> }
        </select>
      </label>
      <label class="grid w-full gap-1.5 sm:w-[260px]">
        <span class="text-[10px] font-bold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('reports.activityPeriod.week') }}</span>
        <select class="w-full min-w-0 rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3] disabled:opacity-60"
          [ngModel]="weekStart" [ngModelOptions]="{standalone: true}" (ngModelChange)="selectWeek($event)" [disabled]="disabled || month === null">
          <option [ngValue]="null">{{ t(month === null ? 'reports.activityPeriod.chooseMonthFirst' : 'reports.activityPeriod.allWeeks') }}</option>
          @for (option of weeks(); track option.value) { <option [ngValue]="option.value">{{ option.label }}</option> }
        </select>
      </label>
    </div>
    @if (showHint) {
      <p class="mt-2 text-xs leading-5 text-[#817589]">{{ t('reports.activityPeriod.hint') }}</p>
    }
  `,
})
export class ActivityPeriodFilterComponent {
  @Input({ required: true }) academicYear = '';
  @Input() month: number | null = null;
  @Input() weekStart: string | null = null;
  @Input() disabled = false;
  @Input() showHint = true;
  @Output() periodChange = new EventEmitter<ActivityPeriodSelection>();
  private readonly language = inject(LanguageService);
  t(key: string): string { return this.language.t(key); }
  months() { return activityMonths(this.academicYear); }
  weeks() { return this.month === null ? [] : activityWeeks(this.academicYear, this.month); }
  selectMonth(month: number | null): void {
    this.periodChange.emit(normalizeActivityPeriod(this.academicYear, {
      month, weekStart: month === this.month ? this.weekStart : null,
    }));
  }
  selectWeek(weekStart: string | null): void {
    this.periodChange.emit(normalizeActivityPeriod(this.academicYear, { month: this.month, weekStart }));
  }
}
