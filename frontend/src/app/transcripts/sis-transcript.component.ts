import { HttpClient, HttpContext } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, combineLatest, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import { BASE_PATH } from '../api/variables';
import { LanguageService } from '../core/language/language.service';
import { SKIP_GLOBAL_API_LOADING } from '../core/loading/api-loading.interceptor';
import { SisStudentMarksComponent } from './sis-student-marks.component';

interface Student {
  id: number;
  fullName: string;
  studentCode: string;
  homeroom: string | null;
}

interface Directory { students: Student[]; page: number; totalPages: number }
type View = { kind: 'directory'; data: Directory } | { kind: 'loading' | 'error' };
@Component({
  selector: 'app-sis-transcript',
  imports: [FormsModule, RouterLink, SisStudentMarksComponent],
  template: `
    <section class="overflow-hidden rounded-xl border border-[#ebe2f1] bg-white shadow-sm">
      <header class="border-b border-[#ebe2f1] bg-[#faf7fc] p-5 sm:p-7">
        <p class="text-sm uppercase tracking-[0.2em] text-[#8a6811]">{{ t('sis.source') }}</p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t('sis.title') }}</h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('sis.subtitle') }}</p>
        @if (studentId()) {
          <a routerLink="/transcripts/sis" class="mt-4 inline-block text-sm font-semibold text-[#6d3a91] hover:underline">← {{ t('sis.back') }}</a>
        } @else {
          <form class="mt-5 flex max-w-lg flex-wrap gap-2 text-sm font-semibold" (ngSubmit)="search()">
            <input type="search" name="search" [(ngModel)]="searchText" maxlength="100"
              [attr.aria-label]="t('sis.searchHint')" [placeholder]="t('sis.searchHint')"
              class="min-w-48 flex-1 rounded-md border border-[#d9cfe2] bg-white px-3 py-2 text-[#32124f]" />
            <button type="submit" class="rounded-md bg-[#32124f] px-4 py-2 text-white hover:bg-[#512874]">{{ t('sis.search') }}</button>
          </form>
        }
      </header>

      @if (studentId(); as id) {
        <app-sis-student-marks [studentId]="id" [showStudent]="true" />
      } @else {
        @let current = view();
        @switch (current.kind) {
          @case ('loading') {
            <p role="status" class="p-6 text-sm text-[#65596d]">{{ t('sis.loading') }}</p>
          }
          @case ('error') {
            <div role="alert" class="flex items-center gap-3 p-6 text-sm text-[#65596d]">
              {{ t('sis.error') }}
              <button class="font-semibold text-[#6d3a91] hover:underline" (click)="reload()">{{ t('sis.retry') }}</button>
            </div>
          }
          @case ('directory') {
            @if (current.data.students.length) {
              <ul class="divide-y divide-[#ebe2f1]">
                @for (student of current.data.students; track student.id) {
                  <li>
                    <a [routerLink]="['/transcripts/sis', student.id]"
                      class="flex items-center justify-between gap-3 px-5 py-4 hover:bg-[#faf7fc] sm:px-7">
                      <div>
                        <p class="text-sm font-semibold text-[#32124f]">{{ student.fullName }}</p>
                        <p class="mt-0.5 text-xs text-[#817589]">
                          {{ student.studentCode }} @if (student.homeroom) { · {{ student.homeroom }} }
                        </p>
                      </div>
                      <span class="text-sm font-semibold text-[#6d3a91]">{{ t('sis.open') }} →</span>
                    </a>
                  </li>
                }
              </ul>
              @if (current.data.totalPages > 1) {
                <nav class="flex flex-wrap items-center justify-end gap-3 border-t border-[#ebe2f1] px-5 py-4 text-sm" [attr.aria-label]="t('sis.pages')">
                  <button class="rounded-md border border-[#d9cfe2] px-3 py-1.5 disabled:opacity-40"
                    [disabled]="current.data.page === 1" (click)="setPage(current.data.page - 1)">{{ t('sis.previous') }}</button>
                  <span>{{ current.data.page }} / {{ current.data.totalPages }}</span>
                  <button class="rounded-md border border-[#d9cfe2] px-3 py-1.5 disabled:opacity-40"
                    [disabled]="current.data.page === current.data.totalPages" (click)="setPage(current.data.page + 1)">{{ t('sis.next') }}</button>
                </nav>
              }
            } @else {
              <p role="status" class="p-6 text-sm text-[#65596d]">{{ t('sis.emptyDirectory') }}</p>
            }
          }
        }
      }
    </section>
  `,
})
export class SisTranscriptComponent {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly base = inject(BASE_PATH) + '/api/transcripts/sis';
  private readonly ids = inject(ActivatedRoute).paramMap.pipe(
    map(params => params.get('studentId')), distinctUntilChanged(),
  );
  readonly studentId = toSignal(this.ids);
  private readonly request = signal({ search: '', page: 1 });
  private readonly attempt = signal(0);
  searchText = '';

  readonly view = toSignal(combineLatest([this.ids, toObservable(this.request), toObservable(this.attempt)]).pipe(
    switchMap(([id, request]) => id ? of<View>({ kind: 'loading' }) :
      this.http.get<Directory>(`${this.base}/students`, {
        context: new HttpContext().set(SKIP_GLOBAL_API_LOADING, true), params: request,
      }).pipe(
        map(data => ({ kind: 'directory', data }) as const),
        startWith<View>({ kind: 'loading' }),
        catchError(() => of<View>({ kind: 'error' })),
      ),
    ),
  ), { initialValue: { kind: 'loading' } as View });

  t(key: string): string { return this.language.t(key); }

  search(): void { this.request.set({ search: this.searchText.trim(), page: 1 }); }
  setPage(page: number): void { this.request.update(request => ({ ...request, page })); }
  reload(): void { this.attempt.update(value => value + 1); }
}
