import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { ClassesService, SISClassDetail, SISClassListItem } from './classes.service';

@Component({
  selector: 'app-classes',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="space-y-7">
      <div>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('classes.eyebrow') }}</p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('classes.title') }}</h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ languageService.t('classes.description') }}</p>
      </div>

      @if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      }

      <section class="overflow-hidden rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-center justify-between gap-4 border-b border-[#ebe6ef] p-6">
          <div>
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('classes.directory') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ totalCount() }} {{ languageService.t('classes.classesLabel') }}</p>
          </div>
          <div class="flex flex-wrap gap-3">
            <input
              class="w-64 rounded-md border border-[#e1dbe6] bg-[#f8f6fa] px-3 py-2.5 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
              [(ngModel)]="search"
              (ngModelChange)="searchChanged()"
              [placeholder]="languageService.t('classes.search')"
            />
            <select
              class="rounded-md border border-[#e1dbe6] bg-[#f8f6fa] px-3 py-2.5 text-sm outline-none focus:border-[#6d3a91]"
              [(ngModel)]="academicYear"
              (ngModelChange)="filterChanged()"
            >
              <option value="">{{ languageService.t('classes.allYears') }}</option>
              @for (year of academicYears(); track year) { <option [value]="year">{{ year }}</option> }
            </select>
          </div>
        </div>

        <div class="grid gap-4 p-6 sm:grid-cols-2 xl:grid-cols-3">
          @for (item of classes(); track item.id) {
            <button
              type="button"
              class="rounded-lg border border-[#e4dce9] p-5 text-left transition hover:-translate-y-0.5 hover:border-[#7b4b99] hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#6d3a91]"
              (click)="openClass(item)"
            >
              <div class="flex items-start justify-between gap-3">
                <div>
                  <p class="text-lg font-semibold text-[#32124f]">{{ item.className }}</p>
                  <p class="mt-1 text-xs font-semibold uppercase tracking-[0.1em] text-[#8a6811]">{{ item.sisClassCode }}</p>
                </div>
                @if (item.gradeLevel) {
                  <span class="rounded-full bg-[#f1edf4] px-3 py-1 text-xs font-bold text-[#5b287d]">{{ languageService.t('classes.grade') }} {{ item.gradeLevel }}</span>
                }
              </div>
              <p class="mt-4 text-sm text-[#6f6877]">{{ item.academicYear }}{{ item.term ? ' · ' + item.term : '' }}{{ item.campus ? ' · ' + item.campus : '' }}</p>
              <div class="mt-5 flex gap-4 border-t border-[#eee9f1] pt-4 text-sm text-[#6f6877]">
                <span><strong class="text-[#32124f]">{{ item.teacherCount }}</strong> {{ languageService.t('classes.teachers') }}</span>
                <span><strong class="text-[#32124f]">{{ item.studentCount }}</strong> {{ languageService.t('classes.students') }}</span>
              </div>
            </button>
          }
          @if (loading()) {
            <p class="col-span-full py-12 text-center text-sm text-[#7c7284]">{{ languageService.t('classes.loading') }}</p>
          } @else if (!classes().length) {
            <p class="col-span-full py-12 text-center text-sm text-[#7c7284]">{{ languageService.t('classes.empty') }}</p>
          }
        </div>

        @if (!loading() && totalCount() > 0) {
          <div class="flex items-center justify-between border-t border-[#ebe6ef] px-6 py-4 text-xs text-[#6f6877]">
            <span>{{ languageService.t('pagination.page') }} {{ page() }} / {{ totalPages() }} · {{ totalCount() }} {{ languageService.t('classes.results') }}</span>
            <div class="flex gap-2">
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 font-semibold text-[#32124f] disabled:opacity-40" [disabled]="page() <= 1" (click)="changePage(-1)">{{ languageService.t('pagination.previous') }}</button>
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 font-semibold text-[#32124f] disabled:opacity-40" [disabled]="page() >= totalPages()" (click)="changePage(1)">{{ languageService.t('pagination.next') }}</button>
            </div>
          </div>
        }
      </section>
    </section>

    @if (selectedId()) {
      <div class="fixed inset-0 z-40 bg-[#24182f]/45" (click)="closeDetail()"></div>
      <aside class="fixed inset-y-0 right-0 z-50 w-full max-w-2xl overflow-y-auto bg-[#faf9f6] shadow-2xl" aria-modal="true" role="dialog">
        <div class="sticky top-0 z-10 flex items-start justify-between border-b border-[#e7e0eb] bg-white p-7">
          <div>
            <p class="text-xs uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('classes.detailEyebrow') }}</p>
            <h3 class="mt-2 text-2xl font-semibold text-[#32124f]">{{ detail()?.className || languageService.t('classes.loading') }}</h3>
            @if (detail()) { <p class="mt-1 text-sm text-[#6f6877]">{{ detail()!.sisClassCode }} · {{ detail()!.academicYear }}</p> }
          </div>
          <button type="button" class="grid h-11 w-11 place-items-center rounded-full border border-[#d8d0df] text-xl text-[#32124f]" (click)="closeDetail()" [attr.aria-label]="languageService.t('classes.close')">×</button>
        </div>
        <div class="space-y-6 p-7">
          @if (detailLoading()) {
            <p class="py-16 text-center text-sm text-[#7c7284]">{{ languageService.t('classes.loadingDetail') }}</p>
          } @else if (detail(); as item) {
            <section class="rounded-lg border border-[#e7e0eb] bg-white">
              <div class="border-b border-[#eee9f1] px-5 py-4"><h4 class="font-semibold text-[#32124f]">{{ languageService.t('classes.teachersTitle') }}</h4></div>
              <div class="divide-y divide-[#eee9f1]">
                @for (teacher of item.teachers; track teacher.id) {
                  <div class="flex items-center gap-3 px-5 py-4">
                    <span class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#f1edf4] text-xs font-bold text-[#5b287d]">{{ initials(teacher.fullName) }}</span>
                    <div class="min-w-0"><p class="font-semibold text-[#32124f]">{{ teacher.fullName }}</p><p class="text-sm text-[#7c7284]">{{ teacher.assignmentRole || teacher.email || teacher.username }}</p></div>
                  </div>
                } @empty { <p class="px-5 py-8 text-sm text-[#7c7284]">{{ languageService.t('classes.noTeachers') }}</p> }
              </div>
            </section>
            <section class="rounded-lg border border-[#e7e0eb] bg-white">
              <div class="border-b border-[#eee9f1] px-5 py-4"><h4 class="font-semibold text-[#32124f]">{{ languageService.t('classes.studentsTitle') }} ({{ item.students.length }})</h4></div>
              <div class="max-h-[34rem] divide-y divide-[#eee9f1] overflow-y-auto">
                @for (student of item.students; track student.id) {
                  <a class="flex items-center gap-3 px-5 py-4 transition hover:bg-[#faf7fc]" [routerLink]="['/students', student.id]">
                    <span class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#fff3c9] text-xs font-bold text-[#7a5b12]">{{ initials(student.fullName) }}</span>
                    <div><p class="font-semibold text-[#32124f]">{{ student.fullName }}</p><p class="text-sm text-[#7c7284]">{{ student.studentCode }}{{ student.homeroom ? ' · ' + student.homeroom : '' }}</p></div>
                  </a>
                } @empty { <p class="px-5 py-8 text-sm text-[#7c7284]">{{ languageService.t('classes.noStudents') }}</p> }
              </div>
            </section>
          }
        </div>
      </aside>
    }
  `,
})
export class ClassesComponent implements OnInit, OnDestroy {
  private readonly service = inject(ClassesService);
  readonly languageService = inject(LanguageService);
  readonly classes = signal<SISClassListItem[]>([]);
  readonly academicYears = signal<string[]>([]);
  readonly page = signal(1);
  readonly pageSize = 12;
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedId = signal<number | null>(null);
  readonly detail = signal<SISClassDetail | null>(null);
  readonly detailLoading = signal(false);
  search = '';
  academicYear = '';
  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { if (this.searchTimer) clearTimeout(this.searchTimer); }

  searchChanged(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => { this.page.set(1); this.load(); }, 300);
  }
  filterChanged(): void { this.page.set(1); this.load(); }
  changePage(delta: number): void { this.page.update(value => value + delta); this.load(); }
  openClass(item: SISClassListItem): void {
    this.selectedId.set(item.id); this.detail.set(null); this.detailLoading.set(true);
    this.service.detail(item.id).subscribe({
      next: value => { this.detail.set(value); this.detailLoading.set(false); },
      error: () => { this.error.set(this.languageService.t('classes.detailError')); this.detailLoading.set(false); this.closeDetail(); },
    });
  }
  closeDetail(): void { this.selectedId.set(null); this.detail.set(null); }
  initials(name: string): string { return name.split(/\s+/).filter(Boolean).slice(-2).map(part => part[0]).join('').toUpperCase(); }

  private load(): void {
    this.loading.set(true); this.error.set(null);
    this.service.list(this.page(), this.pageSize, this.search, this.academicYear).subscribe({
      next: result => {
        this.classes.set(result.items); this.page.set(result.page); this.totalCount.set(result.totalCount);
        this.totalPages.set(result.totalPages); this.academicYears.set(result.academicYears); this.loading.set(false);
      },
      error: () => { this.error.set(this.languageService.t('classes.loadError')); this.loading.set(false); },
    });
  }
}
