import { Component, computed, HostListener, inject, Input, OnChanges, OnDestroy, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import {
  BehaviourCategory,
  BehaviourDrilldownDimension,
  BehaviourGradeSummary,
  BehaviourReportDetail,
  BehaviourReportDetailsQuery,
  BehaviourReportSummary,
  BehaviourReportsService,
  BehaviourStudentSummary,
  BehaviourTypeSummary,
} from './behaviour-reports.service';

interface BehaviourDrilldownContext extends BehaviourReportDetailsQuery {
  title: string;
}

type BehaviourBreakdownTab = 'types' | 'grades';

@Component({
  selector: 'app-behaviour-report',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="space-y-4">
      <article class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
        <header class="border-b border-[#eee8f1] px-5 py-5 sm:px-6">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel }}</p>
              <h3 class="mt-2 text-xl font-semibold text-[#32124f]">{{ languageService.t('reports.behaviour.title') }}</h3>
              <p class="mt-1 max-w-3xl text-sm leading-6 text-[#6f6877]">{{ languageService.t('reports.behaviour.description') }}</p>
            </div>
            <span class="rounded-full bg-[#f2ecf5] px-3 py-1 text-xs font-semibold text-[#5d2d7b]">{{ languageService.t('reports.behaviour.source') }}</span>
          </div>
        </header>

        @if (loading()) {
          <div class="grid min-h-72 place-items-center" role="status">
            <div class="text-center"><span class="mx-auto block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span><p class="mt-3 text-sm font-semibold text-[#5f5368]">{{ languageService.t('reports.loading') }}</p></div>
          </div>
        } @else if (loadFailed()) {
          <div class="grid min-h-64 place-items-center p-8 text-center">
            <div><p class="text-sm font-semibold text-red-700">{{ languageService.t('reports.loadError') }}</p><button class="mt-4 rounded-md border border-red-200 px-4 py-2 text-xs font-semibold text-red-700" type="button" (click)="loadSummary()">{{ languageService.t('reports.retry') }}</button></div>
          </div>
        } @else if (summary(); as data) {
          <div class="grid gap-px border-b border-[#eee8f1] bg-[#eee8f1] sm:grid-cols-2 xl:grid-cols-5">
            <button class="bg-white px-5 py-5 text-left transition hover:bg-[#fdfbfe] focus-visible:outline-2 focus-visible:outline-inset focus-visible:outline-[#6d3a91]" type="button" (click)="openDrilldown('all', '', languageService.t('reports.behaviour.allRecords'))">
              <p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.behaviour.totalRecords') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ formatNumber(data.totalRecords) }}</p>
              <p class="mt-1 text-xs text-[#817589]">{{ languageService.t('reports.behaviour.openDetails') }}</p>
            </button>
            <button class="bg-white px-5 py-5 text-left transition hover:bg-[#fdfbfe] focus-visible:outline-2 focus-visible:outline-inset focus-visible:outline-[#6d3a91]" type="button" (click)="openDrilldown('all', '', languageService.t('reports.behaviour.students'))">
              <p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.behaviour.students') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ formatNumber(data.studentCount) }}</p>
            </button>
            <button class="bg-white px-5 py-5 text-left transition hover:bg-[#fdfbfe] focus-visible:outline-2 focus-visible:outline-inset focus-visible:outline-[#6d3a91]" type="button" (click)="scrollToTypes()">
              <p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.behaviour.behaviourTypes') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ formatNumber(data.behaviourTypeCount) }}</p>
              <p class="mt-1 text-xs text-[#817589]">{{ languageService.t('reports.behaviour.viewBreakdown') }}</p>
            </button>
            <button class="bg-[#f3fbf7] px-5 py-5 text-left transition hover:bg-[#eaf8f1] focus-visible:outline-2 focus-visible:outline-inset focus-visible:outline-emerald-600" type="button" (click)="openDrilldown('category', 'Positive', languageService.t('reports.behaviour.positive'))">
              <p class="text-[10px] font-bold uppercase tracking-[0.12em] text-emerald-700">{{ languageService.t('reports.behaviour.positive') }}</p>
              <p class="mt-2 text-3xl font-semibold text-emerald-800">{{ formatNumber(data.positiveCount) }}</p>
              <p class="mt-1 text-xs text-emerald-700">{{ percent(data.positiveCount, data.totalRecords) }}%</p>
            </button>
            <button class="bg-[#fff7f5] px-5 py-5 text-left transition hover:bg-[#fff0ed] focus-visible:outline-2 focus-visible:outline-inset focus-visible:outline-[#b24a3a]" type="button" (click)="openDrilldown('category', 'Concerning', languageService.t('reports.behaviour.concerning'))">
              <p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#a64032]">{{ languageService.t('reports.behaviour.concerning') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#8f3025]">{{ formatNumber(data.concerningCount) }}</p>
              <p class="mt-1 text-xs text-[#a64032]">{{ percent(data.concerningCount, data.totalRecords) }}%</p>
            </button>
          </div>

          <div class="px-5 py-5 sm:px-6">
            <div class="flex flex-wrap items-end justify-between gap-3">
              <div><h4 class="font-semibold text-[#32124f]">{{ languageService.t('reports.behaviour.balance.title') }}</h4><p class="mt-1 text-sm text-[#817589]">{{ languageService.t('reports.behaviour.balance.hint') }}</p></div>
              @if (data.otherCount) { <button class="text-xs font-semibold text-[#6d3a91] hover:underline" type="button" (click)="openDrilldown('category', 'Other', languageService.t('reports.behaviour.other'))">{{ languageService.t('reports.behaviour.other') }}: {{ formatNumber(data.otherCount) }}</button> }
            </div>
            <div class="mt-4 flex h-5 w-full overflow-hidden rounded-full bg-[#eee8f1]" [attr.aria-label]="languageService.t('reports.behaviour.balance.title')">
              @if (data.positiveCount) { <button class="h-full bg-emerald-500 transition hover:bg-emerald-600" type="button" [style.width.%]="percent(data.positiveCount, data.totalRecords)" [attr.title]="languageService.t('reports.behaviour.positive') + ': ' + data.positiveCount" (click)="openDrilldown('category', 'Positive', languageService.t('reports.behaviour.positive'))"></button> }
              @if (data.concerningCount) { <button class="h-full bg-[#c65a48] transition hover:bg-[#a64032]" type="button" [style.width.%]="percent(data.concerningCount, data.totalRecords)" [attr.title]="languageService.t('reports.behaviour.concerning') + ': ' + data.concerningCount" (click)="openDrilldown('category', 'Concerning', languageService.t('reports.behaviour.concerning'))"></button> }
              @if (data.otherCount) { <button class="h-full bg-[#a79cad] transition hover:bg-[#817589]" type="button" [style.width.%]="percent(data.otherCount, data.totalRecords)" [attr.title]="languageService.t('reports.behaviour.other') + ': ' + data.otherCount" (click)="openDrilldown('category', 'Other', languageService.t('reports.behaviour.other'))"></button> }
            </div>
            <div class="mt-3 flex flex-wrap gap-x-6 gap-y-2 text-xs font-semibold">
              <span class="inline-flex items-center gap-2 text-emerald-800"><span class="h-2.5 w-2.5 rounded-full bg-emerald-500"></span>{{ languageService.t('reports.behaviour.positive') }} {{ formatNumber(data.positiveCount) }}</span>
              <span class="inline-flex items-center gap-2 text-[#8f3025]"><span class="h-2.5 w-2.5 rounded-full bg-[#c65a48]"></span>{{ languageService.t('reports.behaviour.concerning') }} {{ formatNumber(data.concerningCount) }}</span>
              @if (data.otherCount) { <span class="inline-flex items-center gap-2 text-[#625969]"><span class="h-2.5 w-2.5 rounded-full bg-[#a79cad]"></span>{{ languageService.t('reports.behaviour.other') }} {{ formatNumber(data.otherCount) }}</span> }
            </div>
          </div>
        } @else {
          <div class="grid min-h-64 place-items-center p-8 text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</div>
        }
      </article>

      @if (!loading() && summary(); as data) {
        <article id="behaviour-breakdown" class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
          <div class="no-print border-b border-[#e7e0eb] bg-[#faf8fb] px-3 pt-3 sm:px-5" role="tablist" [attr.aria-label]="languageService.t('reports.behaviour.breakdownTabs')">
            <div class="flex gap-1 overflow-x-auto">
              <button
                class="flex min-w-max items-center gap-2 rounded-t-lg border-b-2 px-4 py-3 text-sm font-semibold transition focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-[#6d3a91] sm:px-5"
                type="button"
                role="tab"
                [attr.aria-selected]="breakdownTab() === 'types'"
                [class.border-[#6d3a91]]="breakdownTab() === 'types'"
                [class.bg-white]="breakdownTab() === 'types'"
                [class.text-[#32124f]]="breakdownTab() === 'types'"
                [class.border-transparent]="breakdownTab() !== 'types'"
                [class.text-[#746b7b]]="breakdownTab() !== 'types'"
                (click)="selectBreakdownTab('types')"
              >
                {{ languageService.t('reports.behaviour.types.title') }}
                <span class="rounded-full bg-[#eee5f3] px-2 py-0.5 text-[10px] font-bold text-[#5d2d7b]">{{ formatNumber(data.topTypes.length) }}</span>
              </button>
              <button
                class="flex min-w-max items-center gap-2 rounded-t-lg border-b-2 px-4 py-3 text-sm font-semibold transition focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-[#6d3a91] sm:px-5"
                type="button"
                role="tab"
                [attr.aria-selected]="breakdownTab() === 'grades'"
                [class.border-[#6d3a91]]="breakdownTab() === 'grades'"
                [class.bg-white]="breakdownTab() === 'grades'"
                [class.text-[#32124f]]="breakdownTab() === 'grades'"
                [class.border-transparent]="breakdownTab() !== 'grades'"
                [class.text-[#746b7b]]="breakdownTab() !== 'grades'"
                (click)="selectBreakdownTab('grades')"
              >
                {{ languageService.t('reports.behaviour.grades.title') }}
                <span class="rounded-full bg-[#eee5f3] px-2 py-0.5 text-[10px] font-bold text-[#5d2d7b]">{{ formatNumber(data.byGrade.length) }}</span>
              </button>
            </div>
          </div>

          @if (breakdownTab() === 'types') {
            <div role="tabpanel">
              <header class="border-b border-[#eee8f1] px-5 py-4"><h4 class="font-semibold text-[#32124f]">{{ languageService.t('reports.behaviour.types.title') }}</h4><p class="mt-1 text-sm text-[#817589]">{{ languageService.t('reports.behaviour.types.hint') }}</p></header>
              @if (topTypes().length) {
                <div class="overflow-x-auto"><table class="w-full min-w-[860px] border-collapse text-left">
                <thead class="bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]"><tr><th class="px-5 py-3 font-bold">#</th><th class="px-3 py-3 font-bold">{{ languageService.t('reports.behaviour.type') }}</th><th class="px-3 py-3 font-bold">{{ languageService.t('reports.behaviour.category') }}</th><th class="px-3 py-3 text-right font-bold">{{ languageService.t('reports.behaviour.records') }}</th><th class="px-3 py-3 text-right font-bold">{{ languageService.t('reports.behaviour.students') }}</th><th class="px-5 py-3 text-right font-bold">%</th></tr></thead>
                <tbody class="divide-y divide-[#eee8f1]">
                  @for (row of topTypes(); track row.behaviourType; let rank = $index) {
                    <tr class="cursor-pointer transition hover:bg-[#fdfbfe]" tabindex="0" (click)="openType(row)" (keydown.enter)="openType(row)"><td class="px-5 py-4 text-xs font-bold text-[#8a6811]">{{ rank + 1 }}</td><td class="max-w-3xl px-3 py-4 text-sm font-semibold leading-5 text-[#32124f]">{{ row.behaviourType }}</td><td class="px-3 py-4"><span class="rounded-full px-2.5 py-1 text-[10px] font-bold" [class.bg-emerald-100]="row.category === 'Positive'" [class.text-emerald-800]="row.category === 'Positive'" [class.bg-red-100]="row.category === 'Concerning'" [class.text-red-800]="row.category === 'Concerning'" [class.bg-slate-100]="row.category === 'Other'" [class.text-slate-700]="row.category === 'Other'">{{ categoryLabel(row.category) }}</span></td><td class="px-3 py-4 text-right text-sm font-semibold text-[#32124f]">{{ formatNumber(row.recordCount) }}</td><td class="px-3 py-4 text-right text-sm text-[#625969]">{{ formatNumber(row.studentCount) }}</td><td class="px-5 py-4 text-right text-sm text-[#625969]">{{ row.sharePercentage }}%</td></tr>
                  }
                </tbody>
                </table></div>
                @if (data.topTypes.length > topTypeLimit()) { <div class="border-t border-[#eee8f1] p-3 text-center"><button class="rounded-md px-4 py-2 text-xs font-semibold text-[#5d2d7b] hover:bg-[#f4eef7]" type="button" (click)="toggleAllTypes()">{{ languageService.t(showAllTypes() ? 'reports.behaviour.showLess' : 'reports.behaviour.showAllTypes') }}</button></div> }
              } @else { <p class="p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</p> }
            </div>
          } @else {
            <div role="tabpanel">
              <header class="border-b border-[#eee8f1] px-5 py-4"><h4 class="font-semibold text-[#32124f]">{{ languageService.t('reports.behaviour.grades.title') }}</h4><p class="mt-1 text-sm text-[#817589]">{{ languageService.t('reports.behaviour.grades.hint') }}</p></header>
              @if (data.byGrade.length) {
                <div class="overflow-x-auto"><table class="w-full min-w-[760px] border-collapse text-left">
                <thead class="bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]"><tr><th class="px-5 py-3 font-bold">{{ languageService.t('reports.behaviour.grade') }}</th><th class="px-3 py-3 text-right font-bold">{{ languageService.t('reports.behaviour.records') }}</th><th class="px-3 py-3 text-right font-bold">{{ languageService.t('reports.behaviour.students') }}</th><th class="px-3 py-3 text-right font-bold text-emerald-700">{{ languageService.t('reports.behaviour.positive') }}</th><th class="px-5 py-3 text-right font-bold text-[#a64032]">{{ languageService.t('reports.behaviour.concerning') }}</th></tr></thead>
                <tbody class="divide-y divide-[#eee8f1]">
                  @for (row of data.byGrade; track row.grade) {
                    <tr class="cursor-pointer transition hover:bg-[#fdfbfe]" tabindex="0" (click)="openGrade(row)" (keydown.enter)="openGrade(row)"><td class="px-5 py-4 text-sm font-semibold text-[#32124f]">{{ gradeLabel(row.grade) }}</td><td class="px-3 py-4 text-right text-sm font-semibold text-[#32124f]">{{ formatNumber(row.recordCount) }}</td><td class="px-3 py-4 text-right text-sm text-[#625969]">{{ formatNumber(row.studentCount) }}</td><td class="px-3 py-4 text-right text-sm font-semibold text-emerald-700">{{ formatNumber(row.positiveCount) }}</td><td class="px-5 py-4 text-right text-sm font-semibold text-[#a64032]">{{ formatNumber(row.concerningCount) }}</td></tr>
                  }
                </tbody>
                </table></div>
              } @else { <p class="p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</p> }
            </div>
          }
        </article>

        <article class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
          <header class="flex flex-wrap items-end justify-between gap-3 border-b border-[#eee8f1] px-5 py-4"><div><h4 class="font-semibold text-[#32124f]">{{ languageService.t('reports.behaviour.students.title') }}</h4><p class="mt-1 text-sm text-[#817589]">{{ languageService.t('reports.behaviour.students.hint') }}</p></div><span class="text-xs font-semibold text-[#817589]">{{ formatNumber(data.byStudent.length) }} {{ languageService.t('reports.behaviour.students').toLowerCase() }}</span></header>
          @if (topStudents().length) {
            <div class="overflow-x-auto"><table class="w-full min-w-[820px] border-collapse text-left">
              <thead class="bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]"><tr><th class="px-5 py-3 font-bold">#</th><th class="px-3 py-3 font-bold">{{ languageService.t('reports.table.student') }}</th><th class="px-3 py-3 font-bold">{{ languageService.t('reports.behaviour.grade') }}</th><th class="px-3 py-3 text-right font-bold">{{ languageService.t('reports.behaviour.records') }}</th><th class="px-3 py-3 text-right font-bold text-emerald-700">{{ languageService.t('reports.behaviour.positive') }}</th><th class="px-5 py-3 text-right font-bold text-[#a64032]">{{ languageService.t('reports.behaviour.concerning') }}</th></tr></thead>
              <tbody class="divide-y divide-[#eee8f1]">
                @for (row of topStudents(); track row.key; let rank = $index) {
                  <tr class="cursor-pointer transition hover:bg-[#fdfbfe]" tabindex="0" (click)="openStudent(row)" (keydown.enter)="openStudent(row)"><td class="px-5 py-4 text-xs font-bold text-[#8a6811]">{{ rank + 1 }}</td><td class="px-3 py-4"><p class="text-sm font-semibold text-[#32124f]">{{ row.studentName }}</p><p class="mt-0.5 text-xs text-[#817589]">{{ row.sisUserId || languageService.t('reports.behaviour.unmatched') }}</p></td><td class="px-3 py-4 text-sm text-[#625969]">{{ row.homeroom || gradeLabel(row.grade || '__NO_GRADE__') }}</td><td class="px-3 py-4 text-right text-sm font-semibold text-[#32124f]">{{ formatNumber(row.recordCount) }}</td><td class="px-3 py-4 text-right text-sm font-semibold text-emerald-700">{{ formatNumber(row.positiveCount) }}</td><td class="px-5 py-4 text-right text-sm font-semibold text-[#a64032]">{{ formatNumber(row.concerningCount) }}</td></tr>
                }
              </tbody>
            </table></div>
            @if (data.byStudent.length > studentLimit()) { <div class="border-t border-[#eee8f1] p-3 text-center"><button class="rounded-md px-4 py-2 text-xs font-semibold text-[#5d2d7b] hover:bg-[#f4eef7]" type="button" (click)="toggleAllStudents()">{{ languageService.t(showAllStudents() ? 'reports.behaviour.showLess' : 'reports.behaviour.showAllStudents') }}</button></div> }
          } @else { <p class="p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</p> }
        </article>
      }
    </section>

    @if (drilldownOpen()) {
      <div class="fixed inset-0 z-[90] flex justify-end">
        <button class="absolute inset-0 bg-[#24182f]/45" type="button" [attr.aria-label]="languageService.t('reports.behaviour.details.close')" (click)="closeDrilldown()"></button>
        <section class="relative flex h-full w-full max-w-6xl flex-col bg-white shadow-[-24px_0_70px_rgba(36,24,47,0.24)]" role="dialog" aria-modal="true" [attr.aria-label]="languageService.t('reports.behaviour.details.title')">
          <header class="border-b border-[#ebe6ef] px-5 py-4 sm:px-6">
            <div class="flex items-start justify-between gap-4"><div><p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel }}</p><h3 class="mt-1 text-xl font-semibold text-[#32124f]">{{ drilldownContext()?.title }}</h3><p class="mt-1 text-sm text-[#6f6877]">{{ drilldownCountLabel() }}</p></div><button class="grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#d8d0df] text-xl text-[#32124f] hover:bg-[#f4eef7]" type="button" [attr.aria-label]="languageService.t('reports.behaviour.details.close')" (click)="closeDrilldown()">×</button></div>
            <label class="relative mt-4 block max-w-lg"><span class="sr-only">{{ languageService.t('reports.behaviour.details.search') }}</span><svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8c8193]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg><input class="w-full rounded-md border border-[#d8d0df] bg-white py-2.5 pl-10 pr-9 text-sm outline-none focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3]" type="search" [ngModel]="drilldownSearch()" (ngModelChange)="onDrilldownSearch($event)" [placeholder]="languageService.t('reports.behaviour.details.search')" /></label>
          </header>
          <div class="min-h-0 flex-1 overflow-auto">
            @if (drilldownLoading()) { <div class="grid min-h-64 place-items-center"><span class="block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span></div> }
            @else if (drilldownFailed()) { <div class="grid min-h-64 place-items-center p-8 text-center"><div><p class="text-sm font-semibold text-red-700">{{ languageService.t('reports.loadError') }}</p><button class="mt-3 rounded-md border border-red-200 px-4 py-2 text-xs font-semibold text-red-700" type="button" (click)="loadDrilldown(drilldownPage())">{{ languageService.t('reports.retry') }}</button></div></div> }
            @else if (drilldownItems().length) {
              <table class="w-full min-w-[1180px] border-collapse text-left">
                <thead class="sticky top-0 z-10 bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]"><tr><th class="px-5 py-3 font-bold">{{ languageService.t('reports.behaviour.date') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.table.student') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.behaviour.grade') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.behaviour.type') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.behaviour.category') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.behaviour.notes') }}</th><th class="px-5 py-3 font-bold">{{ languageService.t('reports.behaviour.recordedBy') }}</th></tr></thead>
                <tbody class="divide-y divide-[#eee8f1]">@for (detail of drilldownItems(); track detail.id) { <tr class="align-top hover:bg-[#fdfbfe]"><td class="whitespace-nowrap px-5 py-4 text-xs text-[#625969]">{{ formatDate(detail.incidentDate) }}</td><td class="px-4 py-4">@if (detail.studentProfileId) { <a class="text-sm font-semibold text-[#5d2d7b] hover:underline" [routerLink]="['/students', detail.studentProfileId]">{{ detail.studentName }}</a> } @else { <span class="text-sm font-semibold text-[#32124f]">{{ detail.studentName }}</span> }<p class="mt-0.5 text-xs text-[#817589]">{{ detail.sisUserId || languageService.t('reports.behaviour.unmatched') }}</p></td><td class="px-4 py-4 text-sm text-[#625969]">{{ detail.homeroom || detail.grade || '—' }}</td><td class="max-w-sm px-4 py-4 text-sm font-semibold leading-5 text-[#32124f]">{{ detail.behaviourType }}</td><td class="px-4 py-4"><span class="rounded-full px-2.5 py-1 text-[10px] font-bold" [class.bg-emerald-100]="detail.category === 'Positive'" [class.text-emerald-800]="detail.category === 'Positive'" [class.bg-red-100]="detail.category === 'Concerning'" [class.text-red-800]="detail.category === 'Concerning'" [class.bg-slate-100]="detail.category === 'Other'" [class.text-slate-700]="detail.category === 'Other'">{{ categoryLabel(detail.category) }}</span></td><td class="max-w-md px-4 py-4 text-sm leading-5 text-[#625969]">{{ detail.notes || '—' }}@if (detail.subject || detail.decision) { <p class="mt-1 text-xs text-[#817589]">{{ detail.subject || '' }}{{ detail.subject && detail.decision ? ' · ' : '' }}{{ detail.decision || '' }}</p> }</td><td class="px-5 py-4 text-sm text-[#625969]">{{ detail.recordedBy || '—' }}</td></tr> }</tbody>
              </table>
            } @else { <div class="grid min-h-64 place-items-center p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.behaviour.details.empty') }}</div> }
          </div>
          <footer class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-5 py-3"><span class="text-xs text-[#6f6877]">{{ drilldownPageLabel() }}</span><div class="flex gap-2"><button class="rounded-md border border-[#d8d0df] bg-white px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="drilldownPage() <= 1 || drilldownLoading()" (click)="loadDrilldown(drilldownPage() - 1)">{{ languageService.t('pagination.previous') }}</button><button class="rounded-md border border-[#d8d0df] bg-white px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="drilldownPage() >= drilldownTotalPages() || drilldownLoading()" (click)="loadDrilldown(drilldownPage() + 1)">{{ languageService.t('pagination.next') }}</button></div></footer>
        </section>
      </div>
    }
  `,
})
export class BehaviourReportComponent implements OnChanges, OnDestroy {
  @Input() academicYear = '';
  @Input() term = 'all';
  @Input() periodLabel = '';

  private readonly reports = inject(BehaviourReportsService);
  readonly languageService = inject(LanguageService);
  readonly summary = signal<BehaviourReportSummary | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly breakdownTab = signal<BehaviourBreakdownTab>('types');
  readonly showAllTypes = signal(false);
  readonly showAllStudents = signal(false);
  readonly topTypeLimit = signal(10);
  readonly studentLimit = signal(12);
  readonly topTypes = computed(() => {
    const rows = this.summary()?.topTypes ?? [];
    return this.showAllTypes() ? rows : rows.slice(0, this.topTypeLimit());
  });
  readonly topStudents = computed(() => {
    const rows = this.summary()?.byStudent ?? [];
    return this.showAllStudents() ? rows : rows.slice(0, this.studentLimit());
  });
  readonly drilldownOpen = signal(false);
  readonly drilldownContext = signal<BehaviourDrilldownContext | null>(null);
  readonly drilldownItems = signal<BehaviourReportDetail[]>([]);
  readonly drilldownLoading = signal(false);
  readonly drilldownFailed = signal(false);
  readonly drilldownPage = signal(1);
  readonly drilldownTotalCount = signal(0);
  readonly drilldownTotalPages = signal(0);
  readonly drilldownSearch = signal('');
  private readonly drilldownPageSize = 20;
  private summaryRequestId = 0;
  private drilldownRequestId = 0;
  private searchTimer: number | undefined;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['academicYear'] || changes['term']) {
      this.closeDrilldown();
      this.loadSummary();
    }
  }

  ngOnDestroy(): void {
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
  }

  @HostListener('document:keydown.escape')
  closeOnEscape(): void {
    if (this.drilldownOpen()) this.closeDrilldown();
  }

  loadSummary(): void {
    if (!this.academicYear) return;
    const requestId = ++this.summaryRequestId;
    this.loading.set(true);
    this.loadFailed.set(false);
    this.reports.summary(this.academicYear, this.term).subscribe({
      next: (summary) => {
        if (requestId !== this.summaryRequestId) return;
        this.summary.set(summary);
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.summaryRequestId) return;
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  openDrilldown(dimension: BehaviourDrilldownDimension, value: string, title: string): void {
    this.drilldownContext.set({ dimension, value: value || undefined, title });
    this.drilldownSearch.set('');
    this.drilldownOpen.set(true);
    this.loadDrilldown(1);
  }

  openType(row: BehaviourTypeSummary): void {
    this.openDrilldown('type', row.behaviourType, row.behaviourType);
  }

  openGrade(row: BehaviourGradeSummary): void {
    this.openDrilldown('grade', row.grade, this.gradeLabel(row.grade));
  }

  openStudent(row: BehaviourStudentSummary): void {
    this.drilldownContext.set({
      dimension: 'student',
      studentProfileId: row.studentProfileId ?? undefined,
      studentKey: row.studentProfileId ? undefined : row.key,
      title: row.studentName,
    });
    this.drilldownSearch.set('');
    this.drilldownOpen.set(true);
    this.loadDrilldown(1);
  }

  closeDrilldown(): void {
    this.drilldownRequestId++;
    this.drilldownOpen.set(false);
    this.drilldownContext.set(null);
    this.drilldownItems.set([]);
    this.drilldownLoading.set(false);
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
    this.searchTimer = undefined;
  }

  loadDrilldown(page: number): void {
    const context = this.drilldownContext();
    if (!context) return;
    const requestId = ++this.drilldownRequestId;
    this.drilldownLoading.set(true);
    this.drilldownFailed.set(false);
    this.reports.details(this.academicYear, this.term, {
      ...context,
      search: this.drilldownSearch(),
    }, page, this.drilldownPageSize).subscribe({
      next: (response) => {
        if (requestId !== this.drilldownRequestId) return;
        this.drilldownItems.set(response.items);
        this.drilldownPage.set(response.page);
        this.drilldownTotalCount.set(response.totalCount);
        this.drilldownTotalPages.set(response.totalPages);
        this.drilldownLoading.set(false);
      },
      error: () => {
        if (requestId !== this.drilldownRequestId) return;
        this.drilldownFailed.set(true);
        this.drilldownLoading.set(false);
      },
    });
  }

  onDrilldownSearch(value: string): void {
    this.drilldownSearch.set(value);
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
    this.searchTimer = window.setTimeout(() => {
      this.searchTimer = undefined;
      this.loadDrilldown(1);
    }, 300);
  }

  toggleAllTypes(): void {
    this.showAllTypes.update((value) => !value);
  }

  toggleAllStudents(): void {
    this.showAllStudents.update((value) => !value);
  }

  selectBreakdownTab(tab: BehaviourBreakdownTab): void {
    this.breakdownTab.set(tab);
  }

  scrollToTypes(): void {
    this.breakdownTab.set('types');
    document.getElementById('behaviour-breakdown')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  categoryLabel(category: BehaviourCategory): string {
    return this.languageService.t(`reports.behaviour.category.${category.toLowerCase()}`);
  }

  gradeLabel(grade: string): string {
    return grade === '__NO_GRADE__' ? this.languageService.t('reports.behaviour.noGrade') : grade;
  }

  formatNumber(value: number): string {
    return value.toLocaleString(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US');
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB', {
      day: '2-digit', month: '2-digit', year: 'numeric',
    }).format(new Date(value));
  }

  percent(value: number, total: number): number {
    return total ? Math.round(value * 1000 / total) / 10 : 0;
  }

  drilldownCountLabel(): string {
    return this.languageService.t('reports.behaviour.details.count')
      .replace('{count}', this.formatNumber(this.drilldownTotalCount()));
  }

  drilldownPageLabel(): string {
    return this.languageService.t('pagination.pageOf')
      .replace('{page}', String(this.drilldownPage()))
      .replace('{pages}', String(this.drilldownTotalPages()))
      .replace('{total}', String(this.drilldownTotalCount()));
  }
}
