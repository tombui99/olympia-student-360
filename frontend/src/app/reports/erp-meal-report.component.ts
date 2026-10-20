import { NgClass } from '@angular/common';
import { Component, HostListener, inject, Input, OnChanges, OnDestroy, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { ReportExcelExportService, ReportExportLimitError } from './report-excel-export.service';
import { ReportExportButtonComponent } from './report-export-button.component';
import {
  ErpMealReportsService, MealGroupBy, MealReportDetail, MealReportDetailsPage,
  MealReportOptions, MealReportPage, MealReportQuery, MealReportRow, MealReportType, MealTypeFilter,
} from './erp-meal-reports.service';

type TimeMode = 'period' | 'day' | 'month' | 'range';

@Component({
  selector: 'app-erp-meal-report',
  imports: [FormsModule, NgClass, RouterLink, ReportExportButtonComponent],
  template: `
    <section class="relative overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]" [attr.aria-busy]="loading()">
      <header class="border-b border-[#eee8f1] px-5 py-5 sm:px-6">
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel }}</p>
            <h3 class="mt-2 text-xl font-semibold text-[#32124f]">{{ languageService.t(titleKey()) }}</h3>
            <p class="mt-1 max-w-3xl text-sm leading-6 text-[#6f6877]">{{ languageService.t(descriptionKey()) }}</p>
          </div>
          <div class="flex flex-col items-end gap-2">
            <app-report-export-button [exporting]="exporting()" [disabled]="report === 'ordered-not-eaten' || !data()?.totalCount" (exportRequested)="exportExcel()" />
            <span class="rounded-full bg-[#f2ecf5] px-3 py-1 text-xs font-semibold text-[#5d2d7b]">{{ languageService.t('reports.meals.source') }}</span>
          </div>
        </div>
      </header>

      @if (report === 'ordered-not-eaten') {
        <div class="m-5 rounded-lg border border-amber-200 bg-amber-50 p-5 text-sm leading-6 text-amber-950">
          <p class="font-bold">{{ languageService.t('reports.meals.unavailable') }}</p>
          <p class="mt-2">{{ languageService.t('reports.meals.notEatenGap') }}</p>
        </div>
      } @else {
        @if (report === 'coffee-frequency') {
          <div class="mx-5 mt-5 rounded-lg border border-amber-200 bg-amber-50 p-4 text-xs leading-5 text-amber-950">
            {{ languageService.t('reports.meals.coffeeCaveat') }}
          </div>
        }
        <div class="no-print grid gap-3 border-b border-[#eee8f1] bg-[#fdfbfe] px-5 py-4 sm:grid-cols-2 lg:grid-cols-4">
          <label class="grid gap-1 text-xs font-semibold text-[#625969]">
            {{ languageService.t('reports.meals.timeMode') }}
            <select class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" [(ngModel)]="timeMode" (ngModelChange)="onDateChange()">
              <option value="period">{{ languageService.t('reports.meals.time.period') }}</option>
              <option value="day">{{ languageService.t('reports.meals.time.day') }}</option>
              <option value="month">{{ languageService.t('reports.meals.time.month') }}</option>
              <option value="range">{{ languageService.t('reports.meals.time.range') }}</option>
            </select>
          </label>
          @if (timeMode === 'day') {
            <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.day') }}
              <input class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" type="date" [(ngModel)]="day" (ngModelChange)="onDateChange()" />
            </label>
          } @else if (timeMode === 'month') {
            <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.month') }}
              <input class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" type="month" [(ngModel)]="month" (ngModelChange)="onDateChange()" />
            </label>
          } @else if (timeMode === 'range') {
            <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.from') }}
              <input class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" type="date" [(ngModel)]="from" (ngModelChange)="onDateChange()" />
            </label>
            <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.to') }}
              <input class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" type="date" [(ngModel)]="to" (ngModelChange)="onDateChange()" />
            </label>
          }
          @if (report !== 'coffee-frequency') {
            <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.mealFilter') }}
              <select class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" [(ngModel)]="meal" (ngModelChange)="load(1)">
                <option value="">{{ languageService.t('reports.meals.allMeals') }}</option>
                <option value="breakfast">{{ languageService.t('reports.meals.meal.breakfast') }}</option>
                <option value="lunch">{{ languageService.t('reports.meals.meal.lunch') }}</option>
                <option value="snack">{{ languageService.t('reports.meals.meal.snack') }}</option>
              </select>
            </label>
          }
          <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.groupBy') }}
            <select class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" [(ngModel)]="groupBy" (ngModelChange)="load(1)">
              @for (group of groupOptions; track group) { <option [value]="group">{{ languageService.t('reports.meals.group.' + group) }}</option> }
            </select>
          </label>
          <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.grade') }}
            <select class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" [(ngModel)]="grade" (ngModelChange)="load(1)">
              <option value="">{{ languageService.t('reports.meals.allGrades') }}</option>
              @for (value of options().grades; track value) { <option [value]="value">{{ value }}</option> }
            </select>
          </label>
          <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.class') }}
            <select class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" [(ngModel)]="className" (ngModelChange)="load(1)">
              <option value="">{{ languageService.t('reports.meals.allClasses') }}</option>
              @for (value of options().classes; track value) { <option [value]="value">{{ value }}</option> }
            </select>
          </label>
          <label class="grid gap-1 text-xs font-semibold text-[#625969]">{{ languageService.t('reports.meals.studentSearch') }}
            <input class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm" type="search" [(ngModel)]="search" (ngModelChange)="onSearchChange()" [placeholder]="languageService.t('reports.meals.searchPlaceholder')" />
          </label>
          <div class="flex items-end sm:col-span-2 lg:col-span-4">
            <button class="rounded-md border border-[#d8d0df] bg-white px-4 py-2 text-sm font-semibold text-[#32124f] transition hover:bg-[#f5f0f7]" type="button" [disabled]="loading()" (click)="resetFilters()">
              {{ languageService.t('reports.clearFilters') }}
            </button>
          </div>
        </div>

        @if (loading() && data()) {
          <div class="no-print absolute inset-0 z-10 flex justify-center bg-white/70 pt-36" role="status">
            <div class="flex h-fit items-center gap-3 rounded-full bg-white px-5 py-3 text-sm font-semibold text-[#5f5368] shadow-md"><span class="h-6 w-6 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>{{ languageService.t('reports.loading') }}</div>
          </div>
        }
        @if (failed() && data()) {
          <div class="m-5 rounded-md bg-red-50 p-3 text-sm text-red-700" role="alert">{{ languageService.t('reports.loadError') }} <button class="ml-2 font-semibold underline" type="button" (click)="load(page())">{{ languageService.t('reports.retry') }}</button></div>
        }
        @if (!validDates()) {
          <p class="px-5 py-6 text-sm text-red-700">{{ languageService.t('reports.meals.invalidDates') }}</p>
        } @else if (loading() && !data()) {
          <div class="grid min-h-[360px] place-items-center" role="status"><div class="flex items-center gap-3 text-sm font-semibold text-[#625969]"><span class="h-7 w-7 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>{{ languageService.t('reports.loading') }}</div></div>
        } @else if (failed() && !data()) {
          <div class="px-5 py-10 text-center"><p class="text-sm text-red-700">{{ languageService.t('reports.loadError') }}</p><button class="mt-3 rounded-md border border-red-200 px-3 py-2 text-sm text-red-700" type="button" (click)="load(page())">{{ languageService.t('reports.retry') }}</button></div>
        } @else if (data(); as result) {
          <div class="grid gap-px border-b border-[#eee8f1] bg-[#eee8f1] sm:grid-cols-2" [ngClass]="report === 'coffee-frequency' ? 'lg:grid-cols-3' : ''">
            <div class="bg-white px-5 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t(countLabelKey()) }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(result.totalEvents) }}</p><p class="mt-1 text-xs leading-5 text-[#6f6877]">{{ languageService.t(countMeaningKey()) }}</p></div>
            <div class="bg-white px-5 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.meals.students') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(result.totalStudents) }}</p></div>
            @if (report === 'coffee-frequency') { <div class="bg-white px-5 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.meals.units') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(result.totalUnits) }}</p></div> }
          </div>
          @if (result.items.length) {
            <div class="overflow-x-auto">
              <table class="w-full min-w-[720px] border-collapse text-left">
                <thead class="bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]"><tr>
                  <th class="px-5 py-3 font-bold">{{ languageService.t('reports.meals.group.' + groupBy) }}</th>
                  @if (groupBy === 'student') { <th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.grade') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.class') }}</th> }
                  <th class="px-4 py-3 text-right font-bold">{{ languageService.t(countLabelKey()) }}</th>
                  @if (report === 'coffee-frequency') { <th class="px-4 py-3 text-right font-bold">{{ languageService.t('reports.meals.units') }}</th> }
                  @if (groupBy !== 'student') { <th class="px-4 py-3 text-right font-bold">{{ languageService.t('reports.meals.students') }}</th> }
                  @if (groupBy === 'student') { <th class="px-5 py-3 font-bold">{{ languageService.t('reports.meals.latest') }}</th> }
                </tr></thead>
                <tbody class="divide-y divide-[#eee8f1]">
                  @for (row of result.items; track row.key) { <tr class="hover:bg-[#fdfbfe]">
                    <td class="px-5 py-3 text-sm font-semibold text-[#32124f]">
                      @if (groupBy === 'student' && row.studentProfileId) { <a class="text-[#5d2d7b] hover:underline" [routerLink]="['/students', row.studentProfileId]">{{ row.label }}</a> }
                      @else { <span>{{ row.label }}</span> }
                      @if (groupBy === 'student') { <p class="mt-0.5 text-xs font-normal text-[#817589]">{{ row.erpStudentId }}</p> }
                    </td>
                    @if (groupBy === 'student') { <td class="px-4 py-3 text-sm text-[#625969]">{{ row.grade || '—' }}</td><td class="px-4 py-3 text-sm text-[#625969]">{{ row.className || '—' }}</td> }
                    <td class="px-4 py-3 text-right text-sm font-semibold text-[#32124f]">{{ formatNumber(row.count) }}</td>
                    @if (report === 'coffee-frequency') { <td class="px-4 py-3 text-right text-sm text-[#625969]">{{ formatNumber(row.units) }}</td> }
                    @if (groupBy !== 'student') {
                      <td class="px-5 py-3 text-sm text-[#625969]">
                        <div class="flex items-center justify-end gap-5">
                          <span>{{ formatNumber(row.studentCount) }}</span>
                          <button class="no-print shrink-0 text-xs font-semibold text-[#5d2d7b] underline underline-offset-2 hover:text-[#32124f]" type="button" (click)="openDetails(row)">{{ languageService.t('reports.meals.viewDetails') }}</button>
                        </div>
                      </td>
                    } @else {
                      <td class="px-5 py-3 text-sm text-[#625969]">
                        <div class="flex items-center justify-between gap-5">
                          <span>{{ formatDate(row.latestDay) }}</span>
                          <button class="no-print shrink-0 text-xs font-semibold text-[#5d2d7b] underline underline-offset-2 hover:text-[#32124f]" type="button" (click)="openDetails(row)">{{ languageService.t('reports.meals.viewDetails') }}</button>
                        </div>
                      </td>
                    }
                  </tr> }
                </tbody>
              </table>
            </div>
            <div class="no-print flex items-center justify-between gap-3 border-t border-[#eee8f1] px-5 py-4 text-xs text-[#6f6877]">
              <span>{{ pageLabel(result) }}</span>
              <div class="flex gap-2"><button class="rounded-md border border-[#d8d0df] px-3 py-2 disabled:opacity-40" type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">{{ languageService.t('pagination.previous') }}</button><button class="rounded-md border border-[#d8d0df] px-3 py-2 disabled:opacity-40" type="button" [disabled]="page() >= result.totalPages" (click)="load(page() + 1)">{{ languageService.t('pagination.next') }}</button></div>
            </div>
          } @else { <p class="px-5 py-12 text-center text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</p> }
          <p class="border-t border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#817589]">{{ languageService.t(report === 'coffee-frequency' ? 'reports.meals.coffeeNote' : 'reports.meals.statusNote') }}</p>
        }
      }
    </section>

    @if (detailsOpen()) {
      <div class="fixed inset-0 z-[90] grid place-items-center p-4 sm:p-6">
        <button class="absolute inset-0 bg-[#24182f]/45" type="button" [attr.aria-label]="languageService.t('reports.meals.closeDetails')" (click)="closeDetails()"></button>
        <section class="relative flex max-h-[90vh] w-full max-w-6xl flex-col overflow-hidden rounded-xl bg-white shadow-[0_28px_90px_rgba(36,24,47,0.3)]" role="dialog" aria-modal="true" [attr.aria-label]="languageService.t('reports.meals.details')">
          <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-5 py-4 sm:px-6">
            <div>
              <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ languageService.t(titleKey()) }}</p>
              <h3 class="mt-1 text-xl font-semibold text-[#32124f]">{{ selectedRow()?.label }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('reports.meals.detailCount').replace('{count}', formatNumber(detailsTotalCount())) }}</p>
            </div>
            <button class="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-[#d8d0df] text-xl text-[#32124f] hover:bg-[#f4eef7]" type="button" [attr.aria-label]="languageService.t('reports.meals.closeDetails')" (click)="closeDetails()">×</button>
          </header>
          <div class="min-h-0 flex-1 overflow-auto">
            @if (detailsLoading()) {
              <div class="grid min-h-64 place-items-center" role="status"><span class="block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span></div>
            } @else if (detailsFailed()) {
              <div class="grid min-h-64 place-items-center p-8 text-center"><div><p class="text-sm font-semibold text-red-700">{{ languageService.t('reports.loadError') }}</p><button class="mt-3 rounded-md border border-red-200 px-4 py-2 text-xs font-semibold text-red-700" type="button" (click)="loadDetails(detailsPage())">{{ languageService.t('reports.retry') }}</button></div></div>
            } @else if (details().length) {
              <table class="w-full min-w-[960px] border-collapse text-left">
                <thead class="sticky top-0 bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]"><tr>
                  <th class="px-5 py-3 font-bold">{{ languageService.t('reports.meals.day') }}</th>
                  <th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.group.student') }}</th>
                  <th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.grade') }}</th>
                  <th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.class') }}</th>
                  @if (report === 'coffee-frequency') {
                    <th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.productCode') }}</th>
                    <th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.productName') }}</th>
                    <th class="px-5 py-3 text-right font-bold">{{ languageService.t('reports.meals.units') }}</th>
                  } @else {
                    <th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.meal') }}</th>
                    <th class="px-4 py-3 font-bold">{{ languageService.t('reports.meals.orderStatus') }}</th>
                    <th class="px-5 py-3 font-bold">{{ languageService.t('reports.meals.ticketStatus') }}</th>
                  }
                </tr></thead>
                <tbody class="divide-y divide-[#eee8f1]">
                  @for (detail of details(); track detail.id) { <tr class="align-top hover:bg-[#fdfbfe]">
                    <td class="whitespace-nowrap px-5 py-3 text-sm text-[#625969]">{{ formatDate(detail.eventDay) }}</td>
                    <td class="px-4 py-3">@if (detail.studentProfileId) { <a class="text-sm font-semibold text-[#5d2d7b] hover:underline" [routerLink]="['/students', detail.studentProfileId]">{{ detail.studentName }}</a> } @else { <span class="text-sm font-semibold text-[#32124f]">{{ detail.studentName }}</span> }<p class="mt-0.5 text-xs text-[#817589]">{{ detail.erpStudentId }}</p></td>
                    <td class="px-4 py-3 text-sm text-[#625969]">{{ detail.grade || '—' }}</td>
                    <td class="px-4 py-3 text-sm text-[#625969]">{{ detail.className || '—' }}</td>
                    @if (report === 'coffee-frequency') {
                      <td class="px-4 py-3 text-sm text-[#625969]">{{ detail.productCode || '—' }}</td>
                      <td class="px-4 py-3 text-sm text-[#625969]">{{ detail.productName || '—' }}</td>
                      <td class="px-5 py-3 text-right text-sm font-semibold text-[#32124f]">{{ formatNumber(detail.quantity) }}</td>
                    } @else {
                      <td class="px-4 py-3 text-sm text-[#625969]">{{ detail.meal || '—' }}</td>
                      <td class="px-4 py-3 text-sm text-[#625969]">{{ detail.orderStatus || '—' }}</td>
                      <td class="px-5 py-3 text-sm text-[#625969]">{{ detail.ticketStatus || '—' }}</td>
                    }
                  </tr> }
                </tbody>
              </table>
            } @else {
              <p class="px-5 py-12 text-center text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</p>
            }
          </div>
          @if (!detailsLoading() && !detailsFailed() && detailsTotalPages() > 1) {
            <footer class="flex items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-5 py-3">
              <span class="text-xs text-[#6f6877]">{{ detailsPageLabel() }}</span>
              <div class="flex gap-2"><button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold disabled:opacity-40" type="button" [disabled]="detailsPage() <= 1" (click)="loadDetails(detailsPage() - 1)">{{ languageService.t('pagination.previous') }}</button><button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold disabled:opacity-40" type="button" [disabled]="detailsPage() >= detailsTotalPages()" (click)="loadDetails(detailsPage() + 1)">{{ languageService.t('pagination.next') }}</button></div>
            </footer>
          }
        </section>
      </div>
    }
  `,
})
export class ErpMealReportComponent implements OnChanges, OnDestroy {
  @Input() report: MealReportType = 'ordered-no-ticket';
  @Input() academicYear = '';
  @Input() term = 'all';
  @Input() periodLabel = '';

  readonly languageService = inject(LanguageService);
  private readonly service = inject(ErpMealReportsService);
  private readonly excel = inject(ReportExcelExportService);
  private readonly toast = inject(ToastService);
  readonly groupOptions: MealGroupBy[] = ['student', 'week', 'month', 'grade', 'class'];
  readonly data = signal<MealReportPage | null>(null);
  readonly options = signal<MealReportOptions>({ grades: [], classes: [] });
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly exporting = signal(false);
  readonly page = signal(1);
  readonly detailsOpen = signal(false);
  readonly selectedRow = signal<MealReportRow | null>(null);
  readonly details = signal<MealReportDetail[]>([]);
  readonly detailsLoading = signal(false);
  readonly detailsFailed = signal(false);
  readonly detailsPage = signal(1);
  readonly detailsTotalCount = signal(0);
  readonly detailsTotalPages = signal(0);
  timeMode: TimeMode = 'period';
  groupBy: MealGroupBy = 'student';
  day = '';
  month = '';
  from = '';
  to = '';
  meal: MealTypeFilter | '' = '';
  grade = '';
  className = '';
  search = '';
  private requestId = 0;
  private detailsRequestId = 0;
  private detailContext: { query: MealReportQuery; key: string } | null = null;
  private searchTimer: number | undefined;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['academicYear'] || changes['term']) {
      this.timeMode = 'period';
      const range = this.periodRange();
      this.from = range.from;
      this.to = range.to;
      this.day = range.from;
      this.month = range.from.slice(0, 7);
      this.loadOptions();
    } else if (changes['report']) {
      this.loadOptions();
    }
    this.load(1);
  }

  ngOnDestroy(): void {
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
    this.requestId++;
    this.detailsRequestId++;
  }

  @HostListener('document:keydown.escape')
  closeDetailsOnEscape(): void {
    if (this.detailsOpen()) this.closeDetails();
  }

  titleKey(): string { return `reports.meals.${this.report}.title`; }
  descriptionKey(): string { return `reports.meals.${this.report}.description`; }
  countLabelKey(): string { return `reports.meals.${this.report}.countLabel`; }
  countMeaningKey(): string { return `reports.meals.${this.report}.countMeaning`; }

  onDateChange(): void {
    this.grade = '';
    this.className = '';
    this.loadOptions();
    this.load(1);
  }

  onSearchChange(): void {
    this.closeDetails();
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
    this.searchTimer = window.setTimeout(() => this.load(1), 250);
  }

  resetFilters(): void {
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
    this.searchTimer = undefined;
    const range = this.periodRange();
    this.timeMode = 'period';
    this.groupBy = 'student';
    this.from = range.from;
    this.to = range.to;
    this.day = range.from;
    this.month = range.from.slice(0, 7);
    this.meal = '';
    this.grade = '';
    this.className = '';
    this.search = '';
    this.loadOptions();
    this.load(1);
  }

  validDates(): boolean {
    const { from, to } = this.effectiveRange();
    return /^\d{4}-\d{2}-\d{2}$/.test(from) && /^\d{4}-\d{2}-\d{2}$/.test(to) && from <= to;
  }

  load(page: number): void {
    this.closeDetails();
    const requestId = ++this.requestId;
    if (this.report === 'ordered-not-eaten' || !this.validDates()) {
      this.loading.set(false);
      this.data.set(null);
      return;
    }
    const { from, to } = this.effectiveRange();
    this.loading.set(true);
    this.failed.set(false);
    this.service.summary({
      report: this.report as Exclude<MealReportType, 'ordered-not-eaten'>, groupBy: this.groupBy, from, to,
      meal: this.report === 'coffee-frequency' ? undefined : this.meal || undefined,
      grade: this.grade, className: this.className, search: this.search.trim(),
      page, pageSize: 20,
    }).subscribe({
      next: (response) => {
        if (requestId !== this.requestId) return;
        this.data.set(response);
        this.page.set(response.page);
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.requestId) return;
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  async exportExcel(): Promise<void> {
    if (this.report === 'ordered-not-eaten' || !this.validDates() || !this.data()?.totalCount || this.exporting()) return;
    const { from, to } = this.effectiveRange();
    const baseQuery: MealReportQuery = {
      report: this.report,
      groupBy: this.groupBy,
      from,
      to,
      meal: this.report === 'coffee-frequency' ? undefined : this.meal || undefined,
      grade: this.grade,
      className: this.className,
      search: this.search.trim(),
      page: 1,
      pageSize: 20,
    };
    this.exporting.set(true);
    try {
      const rows = await this.excel.collectPages(async (page, pageSize) => {
        const response = await firstValueFrom(this.service.summary({ ...baseQuery, page, pageSize }, true));
        return { items: response.items, totalCount: response.totalCount, totalPages: response.totalPages };
      });
      const title = this.languageService.t(this.titleKey());
      this.excel.download({
        fileName: `${title}-${from}-${to}`,
        title,
        period: `${from} – ${to}`,
        filters: [
          [this.languageService.t('reports.meals.groupBy'), this.languageService.t(`reports.meals.group.${this.groupBy}`)],
          [this.languageService.t('reports.meals.mealFilter'), this.meal ? this.languageService.t(`reports.meals.meal.${this.meal}`) : this.languageService.t('reports.meals.allMeals')],
          [this.languageService.t('reports.meals.grade'), this.grade || this.languageService.t('reports.meals.allGrades')],
          [this.languageService.t('reports.meals.class'), this.className || this.languageService.t('reports.meals.allClasses')],
          [this.languageService.t('reports.meals.studentSearch'), this.search.trim()],
        ],
        columns: [
          { header: this.languageService.t(`reports.meals.group.${this.groupBy}`), value: row => row.label },
          { header: 'ERP / SIS ID', value: row => row.erpStudentId },
          { header: this.languageService.t('reports.meals.grade'), value: row => row.grade },
          { header: this.languageService.t('reports.meals.class'), value: row => row.className },
          { header: this.languageService.t('reports.meals.from'), value: row => row.periodStart },
          { header: this.languageService.t('reports.meals.latest'), value: row => row.latestDay },
          { header: this.languageService.t(this.countLabelKey()), value: row => row.count },
          { header: this.languageService.t('reports.meals.units'), value: row => row.units },
          { header: this.languageService.t('reports.meals.students'), value: row => row.studentCount },
        ],
        rows,
      });
    } catch (error) {
      this.toast.error(this.languageService.t(error instanceof ReportExportLimitError ? 'reports.exportLimit' : 'reports.exportError'));
    } finally {
      this.exporting.set(false);
    }
  }

  pageLabel(result: MealReportPage): string {
    if (!result.totalCount) return '0';
    const start = (result.page - 1) * result.pageSize + 1;
    return `${start}–${Math.min(start + result.items.length - 1, result.totalCount)} / ${result.totalCount}`;
  }

  openDetails(row: MealReportRow): void {
    if (this.report === 'ordered-not-eaten' || !this.validDates()) return;
    const { from, to } = this.effectiveRange();
    this.detailContext = {
      key: row.key,
      query: {
        report: this.report, groupBy: this.groupBy, from, to,
        meal: this.report === 'coffee-frequency' ? undefined : this.meal || undefined,
        grade: this.grade, className: this.className, search: this.search.trim(),
        page: 1, pageSize: 20,
      },
    };
    this.selectedRow.set(row);
    this.detailsOpen.set(true);
    this.loadDetails(1);
  }

  closeDetails(): void {
    this.detailsRequestId++;
    this.detailContext = null;
    this.detailsOpen.set(false);
    this.selectedRow.set(null);
    this.details.set([]);
    this.detailsLoading.set(false);
  }

  loadDetails(page: number): void {
    const context = this.detailContext;
    if (!context) return;
    const requestId = ++this.detailsRequestId;
    this.detailsLoading.set(true);
    this.detailsFailed.set(false);
    this.service.details({ ...context.query, page }, context.key).subscribe({
      next: (response: MealReportDetailsPage) => {
        if (requestId !== this.detailsRequestId) return;
        this.details.set(response.items);
        this.detailsPage.set(response.page);
        this.detailsTotalCount.set(response.totalCount);
        this.detailsTotalPages.set(response.totalPages);
        this.detailsLoading.set(false);
      },
      error: () => {
        if (requestId !== this.detailsRequestId) return;
        this.detailsFailed.set(true);
        this.detailsLoading.set(false);
      },
    });
  }

  detailsPageLabel(): string {
    const total = this.detailsTotalCount();
    if (!total) return '0';
    const start = (this.detailsPage() - 1) * 20 + 1;
    return `${start}–${Math.min(start + this.details().length - 1, total)} / ${total}`;
  }

  formatNumber(value: number): string { return new Intl.NumberFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US').format(value); }
  formatDate(value?: string | null): string {
    if (!value) return '—';
    const [year, month, day] = value.slice(0, 10).split('-');
    return `${day}/${month}/${year}`;
  }

  private loadOptions(): void {
    if (this.report === 'ordered-not-eaten' || !this.validDates()) return;
    const { from, to } = this.effectiveRange();
    this.service.options(from, to).subscribe({
      next: (values) => this.options.set(values),
      error: () => this.options.set({ grades: [], classes: [] }),
    });
  }

  private effectiveRange(): { from: string; to: string } {
    if (this.timeMode === 'day') return { from: this.day, to: this.day };
    if (this.timeMode === 'month') {
      const [year, month] = this.month.split('-').map(Number);
      return { from: `${this.month}-01`, to: `${this.month}-${String(new Date(year, month, 0).getDate()).padStart(2, '0')}` };
    }
    return { from: this.from, to: this.to };
  }

  private periodRange(): { from: string; to: string } {
    const start = Number(this.academicYear.slice(0, 4)) || new Date().getFullYear();
    if (this.term === 'hk1') return { from: `${start}-09-01`, to: `${start + 1}-01-18` };
    if (this.term === 'hk2') return { from: `${start + 1}-01-19`, to: `${start + 1}-05-31` };
    return { from: `${start}-09-01`, to: `${start + 1}-08-31` };
  }
}
