import { ChangeDetectorRef, Component, EventEmitter, inject, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../core/language/language.service';
import { StudentClassificationSettings, StudentsService } from './students.service';

@Component({
  selector: 'app-classification-settings-modal',
  imports: [FormsModule],
  template: `
    @if (open) {
      <div class="fixed inset-0 z-50 grid place-items-center p-4">
        <button class="absolute inset-0 bg-[#24182f]/45" type="button" (click)="closed.emit()"></button>
        <section class="relative flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-xl bg-white shadow-[0_28px_90px_rgba(36,24,47,0.3)]" role="dialog" aria-modal="true">
          <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
            <div>
              <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('classificationSettings.' + mode + '.eyebrow') }}</p>
              <h3 class="mt-1 text-2xl font-semibold text-[#32124f]">{{ languageService.t('classificationSettings.' + mode + '.title') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('classificationSettings.' + mode + '.hint') }}</p>
            </div>
            <button class="grid h-9 w-9 place-items-center rounded-full border border-[#d8d0df] text-[#32124f]" type="button" (click)="closed.emit()">x</button>
          </div>

          <form class="min-h-0 overflow-y-auto" (ngSubmit)="save()">
            <div class="space-y-5 p-6">
              @if (error) {
                <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error }}</p>
              }
              @if (loading || !draft) {
                <p class="py-12 text-center text-sm text-[#7c7284]">{{ languageService.t('classificationSettings.loading') }}</p>
              } @else if (mode === 'risk') {
                <section class="rounded-lg border border-[#f0d5d5] bg-red-50/40 p-5">
                  <h4 class="font-semibold text-red-800">{{ languageService.t('classificationSettings.risk.highSummary') }}</h4>
                  <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('classificationSettings.risk.anyRule') }}</p>
                  <ul class="mt-3 list-disc space-y-2 pl-5 text-sm leading-6 text-[#4f4756]">
                    <li>{{ rule('classificationSettings.risk.behaviourRule', { count: draft.highRiskBehaviourCount, days: draft.riskWindowDays }) }}</li>
                    <li>{{ rule('classificationSettings.risk.busRule', { count: draft.highRiskBusViolationCount, days: draft.riskWindowDays }) }}</li>
                    <li>{{ rule('classificationSettings.risk.canvasRule', { score: draft.highRiskCanvasAverageBelow }) }}</li>
                  </ul>
                  <div class="mt-4 rounded-md bg-white p-3 text-sm leading-6 text-[#4f4756] shadow-[inset_0_0_0_1px_#ebe6ef]">
                    <p><strong>{{ languageService.t('atRisk.level.Medium') }}:</strong> {{ rule('classificationSettings.risk.mediumRule', { behaviour: draft.highRiskBehaviourCount, bus: draft.highRiskBusViolationCount, days: draft.riskWindowDays }) }}</p>
                    <p class="mt-1"><strong>{{ languageService.t('atRisk.level.None') }}:</strong> {{ languageService.t('classificationSettings.risk.noneRule') }}</p>
                  </div>
                </section>

                <fieldset class="rounded-lg border border-[#ebe6ef] p-5">
                  <legend class="px-2 font-semibold text-[#32124f]">{{ languageService.t('classificationSettings.risk.editRules') }}</legend>
                  <div class="grid gap-4 sm:grid-cols-2">
                    <label class="field"><span>{{ languageService.t('classificationSettings.risk.windowDays') }}</span><input type="number" min="1" max="365" name="riskWindowDays" [(ngModel)]="draft.riskWindowDays" required /></label>
                    <label class="field"><span>{{ languageService.t('classificationSettings.risk.behaviourCount') }}</span><input type="number" min="0" max="1000" name="highRiskBehaviourCount" [(ngModel)]="draft.highRiskBehaviourCount" required /></label>
                    <label class="field"><span>{{ languageService.t('classificationSettings.risk.busCount') }}</span><input type="number" min="0" max="1000" name="highRiskBusViolationCount" [(ngModel)]="draft.highRiskBusViolationCount" required /></label>
                    <label class="field"><span>{{ languageService.t('classificationSettings.risk.canvasBelow') }}</span><input type="number" min="0" max="100" step="0.1" name="highRiskCanvasAverageBelow" [(ngModel)]="draft.highRiskCanvasAverageBelow" required /></label>
                  </div>
                </fieldset>
              } @else {
                <section class="rounded-lg border border-[#cfe4dd] bg-[#f2faf7] p-5">
                  <h4 class="font-semibold text-[#245b59]">{{ languageService.t('classificationSettings.commendation.summary') }}</h4>
                  <p class="mt-2 text-sm leading-6 text-[#4f4756]">{{ rule('classificationSettings.commendation.rule', {
                    positive: draft.commendationPositiveBehaviourCount,
                    days: draft.commendationWindowDays,
                    canvas: draft.commendationCanvasAverageAtLeast,
                    bus: draft.commendationMaxBusViolations
                  }) }}</p>
                </section>

                <fieldset class="rounded-lg border border-[#ebe6ef] p-5">
                  <legend class="px-2 font-semibold text-[#32124f]">{{ languageService.t('classificationSettings.commendation.editRules') }}</legend>
                  <div class="grid gap-4 sm:grid-cols-2">
                    <label class="field"><span>{{ languageService.t('classificationSettings.commendation.windowDays') }}</span><input type="number" min="1" max="365" name="commendationWindowDays" [(ngModel)]="draft.commendationWindowDays" required /></label>
                    <label class="field"><span>{{ languageService.t('classificationSettings.commendation.positiveCount') }}</span><input type="number" min="1" max="1000" name="commendationPositiveBehaviourCount" [(ngModel)]="draft.commendationPositiveBehaviourCount" required /></label>
                    <label class="field"><span>{{ languageService.t('classificationSettings.commendation.canvasAtLeast') }}</span><input type="number" min="0" max="100" step="0.1" name="commendationCanvasAverageAtLeast" [(ngModel)]="draft.commendationCanvasAverageAtLeast" required /></label>
                    <label class="field"><span>{{ languageService.t('classificationSettings.commendation.maxBus') }}</span><input type="number" min="0" max="1000" name="commendationMaxBusViolations" [(ngModel)]="draft.commendationMaxBusViolations" required /></label>
                  </div>
                </fieldset>
              }
            </div>
            <div class="flex justify-end gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
              <button class="rounded-md border border-[#d8d0df] px-5 py-2.5 text-sm font-semibold text-[#32124f]" type="button" (click)="closed.emit()">{{ languageService.t('workspace.cancel') }}</button>
              <button class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-40" type="submit" [disabled]="loading || saving || !draft">{{ languageService.t(saving ? 'classificationSettings.saving' : 'workspace.save') }}</button>
            </div>
          </form>
        </section>
      </div>
    }
  `,
  styles: [`
    .field { display: block; font-size: .8rem; font-weight: 600; color: #4f4756; }
    .field input { display: block; width: 100%; margin-top: .45rem; border: 1px solid #d8d0df; border-radius: .375rem; background: white; padding: .65rem .75rem; outline: none; }
    .field input:focus { border-color: #6d3a91; box-shadow: 0 0 0 3px rgba(109,58,145,.1); }
  `],
})
export class ClassificationSettingsModalComponent implements OnChanges {
  private readonly studentsService = inject(StudentsService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly languageService = inject(LanguageService);

  @Input() open = false;
  @Input() mode: 'risk' | 'commendation' = 'risk';
  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly saved = new EventEmitter<void>();

  draft: StudentClassificationSettings | null = null;
  loading = false;
  saving = false;
  error: string | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue) this.load();
  }

  rule(key: string, values: Record<string, string | number>): string {
    return Object.entries(values).reduce(
      (text, [name, value]) => text.replaceAll(`{${name}}`, String(value)),
      this.languageService.t(key),
    );
  }

  save(): void {
    if (!this.draft) return;
    this.saving = true;
    this.error = null;
    this.studentsService.saveClassificationSettings(this.draft).subscribe({
      next: (settings) => {
        this.draft = { ...settings };
        this.saving = false;
        this.changeDetector.markForCheck();
        this.saved.emit();
        this.closed.emit();
      },
      error: () => {
        this.saving = false;
        this.error = this.languageService.t('classificationSettings.saveError');
        this.changeDetector.markForCheck();
      },
    });
  }

  private load(): void {
    this.loading = true;
    this.error = null;
    this.studentsService.classificationSettings().subscribe({
      next: (settings) => {
        this.draft = { ...settings };
        this.loading = false;
        this.changeDetector.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.error = this.languageService.t('classificationSettings.loadError');
        this.changeDetector.markForCheck();
      },
    });
  }
}
