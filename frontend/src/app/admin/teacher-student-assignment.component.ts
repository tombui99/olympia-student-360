import { NgFor, NgIf } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../core/language/language.service';
import {
  AssignmentStudent,
  AssignmentTeacher,
  TeacherStudentAssignmentService,
} from './teacher-student-assignment.service';

@Component({
  selector: 'app-teacher-student-assignment',
  imports: [FormsModule, NgFor, NgIf],
  template: `
    <section class="space-y-7">
      <div>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
          {{ languageService.t('teacherAssignments.eyebrow') }}
        </p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">
          {{ languageService.t('teacherAssignments.title') }}
        </h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">
          {{ languageService.t('teacherAssignments.description') }}
        </p>
      </div>

      <p *ngIf="error()" class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
        {{ error() }}
      </p>
      <p
        *ngIf="success()"
        class="rounded-md border border-[#d8ead0] bg-[#f5fff0] px-4 py-3 text-sm text-[#3f6f24]"
      >
        {{ success() }}
      </p>

      <section
        class="overflow-hidden rounded-xl border border-[#e3dce8] bg-white shadow-[0_18px_50px_rgba(50,18,79,0.08)] xl:grid xl:min-h-[680px] xl:grid-cols-[300px_minmax(0,1fr)]"
      >
        <aside class="border-b border-[#ebe6ef] xl:border-b-0 xl:border-r">
          <div class="border-b border-[#ebe6ef] px-5 py-5">
            <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a7d93]">
              {{ languageService.t('teacherAssignments.teacherList') }}
            </p>
            <p class="mt-1 text-sm text-[#6f6877]">
              {{ teachers().length }} {{ languageService.t('teacherAssignments.teachers') }}
            </p>
            <label class="relative mt-4 block">
              <span class="sr-only">{{ languageService.t('teacherAssignments.searchTeachers') }}</span>
              <svg
                class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#94889d]"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                aria-hidden="true"
              >
                <circle cx="11" cy="11" r="7"></circle>
                <path d="m20 20-3.5-3.5"></path>
              </svg>
              <input
                class="w-full rounded-md border border-[#e1dbe6] bg-[#f8f6fa] py-2.5 pl-9 pr-3 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                [(ngModel)]="teacherSearch"
                [placeholder]="languageService.t('teacherAssignments.searchTeachers')"
              />
            </label>
          </div>

          <div class="max-h-[500px] space-y-2 overflow-y-auto p-3 xl:max-h-[580px]">
            <button
              *ngFor="let teacher of filteredTeachers()"
              class="flex w-full items-center gap-3 rounded-lg border px-3 py-3 text-left transition"
              [class.border-[#5b287d]]="teacher.id === selectedTeacherId()"
              [class.bg-[#4b1d6b]]="teacher.id === selectedTeacherId()"
              [class.text-white]="teacher.id === selectedTeacherId()"
              [class.border-[#ebe6ef]]="teacher.id !== selectedTeacherId()"
              [class.bg-white]="teacher.id !== selectedTeacherId()"
              [class.hover:bg-[#faf7fc]]="teacher.id !== selectedTeacherId()"
              type="button"
              (click)="selectTeacher(teacher)"
            >
              <span
                class="grid h-10 w-10 shrink-0 place-items-center rounded-full text-sm font-bold"
                [class.bg-[#f3c246]]="teacher.id === selectedTeacherId()"
                [class.text-[#32124f]]="teacher.id === selectedTeacherId()"
                [class.bg-[#f1edf4]]="teacher.id !== selectedTeacherId()"
                [class.text-[#6f6877]]="teacher.id !== selectedTeacherId()"
              >
                {{ initials(teacher.fullName) }}
              </span>
              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm font-semibold">{{ teacher.fullName }}</span>
                <span
                  class="mt-0.5 block truncate text-xs"
                  [class.text-[#d8c9e3]]="teacher.id === selectedTeacherId()"
                  [class.text-[#7c7284]]="teacher.id !== selectedTeacherId()"
                >
                  {{ teacher.email || teacher.username }}
                </span>
              </span>
              <span
                class="rounded-full px-2 py-1 text-xs font-bold"
                [class.bg-white/15]="teacher.id === selectedTeacherId()"
                [class.bg-[#f3c246]/25]="teacher.id !== selectedTeacherId()"
                [class.text-[#7a5b12]]="teacher.id !== selectedTeacherId()"
              >
                {{ teacher.assignedStudentCount }}
              </span>
            </button>

            <div *ngIf="loading()" class="px-3 py-10 text-center text-sm text-[#7c7284]">
              {{ languageService.t('teacherAssignments.loading') }}
            </div>
            <div
              *ngIf="!loading() && !filteredTeachers().length"
              class="px-3 py-10 text-center text-sm text-[#7c7284]"
            >
              {{ languageService.t('teacherAssignments.noTeachers') }}
            </div>
          </div>
        </aside>

        <div *ngIf="selectedTeacher() as teacher; else selectTeacherPrompt" class="min-w-0">
          <div class="flex flex-wrap items-center justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
            <div>
              <h3 class="text-xl font-semibold text-[#32124f]">
                {{ languageService.t('teacherAssignments.detailsTitle') }}
              </h3>
              <p class="mt-1 text-sm text-[#6f6877]">
                {{ languageService.t('teacherAssignments.detailsHint') }}
                <span class="font-semibold text-[#32124f]">{{ teacher.fullName }}</span>.
              </p>
            </div>
            <button
              class="rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white shadow-[0_8px_20px_rgba(50,18,79,0.2)] transition hover:bg-[#4b2070] disabled:cursor-not-allowed disabled:opacity-50"
              type="button"
              [disabled]="saving() || !isDirty()"
              (click)="saveAssignments()"
            >
              {{
                saving()
                  ? languageService.t('teacherAssignments.saving')
                  : languageService.t('teacherAssignments.save')
              }}
            </button>
          </div>

          <div class="grid gap-5 bg-[#faf9f6] p-5 md:grid-cols-2">
            <section class="flex min-h-[520px] min-w-0 flex-col overflow-hidden rounded-xl border border-[#e1dbe6] bg-white shadow-sm">
              <div class="flex items-start justify-between gap-3 border-b border-[#ebe6ef] bg-[#f8f6fa] px-4 py-4">
                <div>
                  <h4 class="font-semibold text-[#32124f]">
                    {{ languageService.t('teacherAssignments.availableTitle') }}
                  </h4>
                  <p class="mt-1 text-xs text-[#7c7284]">
                    {{ allAvailableStudents().length }}
                    {{ languageService.t('teacherAssignments.students') }}
                  </p>
                </div>
                <button
                  class="text-sm font-semibold text-[#5b287d] transition hover:text-[#32124f] disabled:opacity-40"
                  type="button"
                  [disabled]="!allAvailableStudents().length"
                  (click)="assignAll()"
                >
                  {{ languageService.t('teacherAssignments.assignAll') }} →
                </button>
              </div>

              <div class="border-b border-[#ebe6ef] p-4">
                <input
                  class="w-full rounded-md border-0 bg-[#f3f0f6] px-4 py-3 text-sm outline-none ring-1 ring-transparent transition focus:ring-[#6d3a91]"
                  [(ngModel)]="availableSearch"
                  [placeholder]="languageService.t('teacherAssignments.searchAvailable')"
                />
              </div>

              <div class="min-h-0 flex-1 space-y-2 overflow-y-auto p-3">
                <article
                  *ngFor="let student of availableStudents()"
                  class="flex items-center gap-3 rounded-lg border border-[#ebe6ef] p-3 transition hover:border-[#d3c6db] hover:bg-[#fdfcff]"
                >
                  <span class="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-[#f1edf4] text-xs font-bold text-[#5b287d]">
                    {{ initials(student.fullName) }}
                  </span>
                  <span class="min-w-0 flex-1">
                    <span class="block truncate text-sm font-semibold text-[#30223a]">{{ student.fullName }}</span>
                    <span class="mt-0.5 block truncate text-xs text-[#7c7284]">
                      {{ student.studentCode }} · {{ languageService.t('teacherAssignments.grade') }}
                      {{ student.gradeLevel || '-' }} · {{ student.homeroom || '-' }}
                    </span>
                  </span>
                  <button
                    class="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-[#f3c246]/25 font-bold text-[#5b287d] transition hover:bg-[#f3c246]"
                    type="button"
                    [attr.aria-label]="languageService.t('teacherAssignments.assignStudent')"
                    (click)="assignStudent(student.id)"
                  >
                    →
                  </button>
                </article>
                <p *ngIf="!availableStudents().length" class="px-4 py-12 text-center text-sm text-[#7c7284]">
                  {{ languageService.t('teacherAssignments.noAvailableStudents') }}
                </p>
              </div>
            </section>

            <section class="flex min-h-[520px] min-w-0 flex-col overflow-hidden rounded-xl border border-[#d9c9e4] bg-white shadow-sm">
              <div class="flex items-start justify-between gap-3 border-b border-[#e5d8ec] bg-[#f7f0fb] px-4 py-4">
                <div>
                  <h4 class="font-semibold text-[#32124f]">
                    {{ languageService.t('teacherAssignments.assignedTitle') }}
                  </h4>
                  <p class="mt-1 text-xs text-[#6d3a91]">
                    {{ assignedStudents().length }}
                    {{ languageService.t('teacherAssignments.students') }}
                  </p>
                </div>
                <button
                  class="text-sm font-semibold text-red-600 transition hover:text-red-800 disabled:opacity-40"
                  type="button"
                  [disabled]="!assignedStudents().length"
                  (click)="removeAll()"
                >
                  ← {{ languageService.t('teacherAssignments.removeAll') }}
                </button>
              </div>

              <div class="border-b border-[#ebe6ef] p-4">
                <input
                  class="w-full rounded-md border-0 bg-[#f3f0f6] px-4 py-3 text-sm outline-none ring-1 ring-transparent transition focus:ring-[#6d3a91]"
                  [(ngModel)]="assignedSearch"
                  [placeholder]="languageService.t('teacherAssignments.searchAssigned')"
                />
              </div>

              <div class="min-h-0 flex-1 space-y-2 overflow-y-auto p-3">
                <article
                  *ngFor="let student of assignedStudents()"
                  class="flex items-center gap-3 rounded-lg border border-[#e5ddec] p-3 transition hover:border-[#cdb9da] hover:bg-[#fdfaff]"
                >
                  <span class="h-2.5 w-2.5 shrink-0 rounded-full bg-emerald-500"></span>
                  <span class="min-w-0 flex-1">
                    <span class="block truncate text-sm font-semibold text-[#30223a]">{{ student.fullName }}</span>
                    <span class="mt-0.5 block truncate text-xs text-[#7c7284]">
                      {{ student.studentCode }} · {{ languageService.t('teacherAssignments.grade') }}
                      {{ student.gradeLevel || '-' }} · {{ student.homeroom || '-' }}
                    </span>
                  </span>
                  <button
                    class="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-red-50 font-bold text-red-600 transition hover:bg-red-100"
                    type="button"
                    [attr.aria-label]="languageService.t('teacherAssignments.removeStudent')"
                    (click)="removeStudent(student.id)"
                  >
                    ←
                  </button>
                </article>
                <p *ngIf="!assignedStudents().length" class="px-4 py-12 text-center text-sm text-[#7c7284]">
                  {{ languageService.t('teacherAssignments.noAssignedStudents') }}
                </p>
              </div>
            </section>
          </div>
        </div>

        <ng-template #selectTeacherPrompt>
          <div class="grid min-h-[540px] place-items-center p-8 text-center">
            <div>
              <div class="mx-auto grid h-14 w-14 place-items-center rounded-full bg-[#f3c246]/25 text-xl text-[#5b287d]">↔</div>
              <h3 class="mt-4 text-lg font-semibold text-[#32124f]">
                {{ languageService.t('teacherAssignments.selectTeacherTitle') }}
              </h3>
              <p class="mt-2 text-sm text-[#7c7284]">
                {{ languageService.t('teacherAssignments.selectTeacherBody') }}
              </p>
            </div>
          </div>
        </ng-template>
      </section>
    </section>
  `,
})
export class TeacherStudentAssignmentComponent implements OnInit {
  private readonly assignmentService = inject(TeacherStudentAssignmentService);
  readonly languageService = inject(LanguageService);

  readonly teachers = signal<AssignmentTeacher[]>([]);
  readonly students = signal<AssignmentStudent[]>([]);
  readonly selectedTeacherId = signal<string | null>(null);
  readonly draftStudentIds = signal<number[]>([]);
  readonly originalStudentIds = signal<number[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  teacherSearch = '';
  availableSearch = '';
  assignedSearch = '';

  readonly selectedTeacher = computed(
    () => this.teachers().find((teacher) => teacher.id === this.selectedTeacherId()) ?? null,
  );

  ngOnInit(): void {
    this.load();
  }

  load(preferredTeacherId?: string): void {
    this.loading.set(true);
    this.assignmentService.load().subscribe({
      next: (result) => {
        this.teachers.set(result.teachers);
        this.students.set(result.students);
        this.loading.set(false);
        const teacher =
          result.teachers.find((candidate) => candidate.id === preferredTeacherId) ??
          result.teachers[0];
        if (teacher) {
          this.selectTeacher(teacher, false);
        } else {
          this.selectedTeacherId.set(null);
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set(this.languageService.t('teacherAssignments.loadError'));
      },
    });
  }

  selectTeacher(teacher: AssignmentTeacher, clearMessages = true): void {
    this.selectedTeacherId.set(teacher.id);
    const ids = this.students()
      .filter((student) => student.teacherId === teacher.id)
      .map((student) => student.id);
    this.originalStudentIds.set(ids);
    this.draftStudentIds.set([...ids]);
    this.availableSearch = '';
    this.assignedSearch = '';
    if (clearMessages) {
      this.error.set(null);
      this.success.set(null);
    }
  }

  filteredTeachers(): AssignmentTeacher[] {
    const query = this.normalize(this.teacherSearch);
    if (!query) {
      return this.teachers();
    }
    return this.teachers().filter((teacher) =>
      this.normalize(`${teacher.fullName} ${teacher.username} ${teacher.email ?? ''}`).includes(query),
    );
  }

  allAvailableStudents(): AssignmentStudent[] {
    const teacherId = this.selectedTeacherId();
    const selected = new Set(this.draftStudentIds());
    return this.students().filter(
      (student) =>
        (student.teacherId == null || student.teacherId === teacherId) && !selected.has(student.id),
    );
  }

  availableStudents(): AssignmentStudent[] {
    const query = this.normalize(this.availableSearch);
    return this.allAvailableStudents().filter((student) => this.matchesStudent(student, query));
  }

  assignedStudents(): AssignmentStudent[] {
    const query = this.normalize(this.assignedSearch);
    const selected = new Set(this.draftStudentIds());
    return this.students().filter(
      (student) => selected.has(student.id) && this.matchesStudent(student, query),
    );
  }

  assignStudent(studentId: number): void {
    if (!this.draftStudentIds().includes(studentId)) {
      this.draftStudentIds.update((ids) => [...ids, studentId]);
    }
    this.clearMessages();
  }

  removeStudent(studentId: number): void {
    this.draftStudentIds.update((ids) => ids.filter((id) => id !== studentId));
    this.clearMessages();
  }

  assignAll(): void {
    const ids = new Set(this.draftStudentIds());
    this.allAvailableStudents().forEach((student) => ids.add(student.id));
    this.draftStudentIds.set([...ids]);
    this.clearMessages();
  }

  removeAll(): void {
    this.draftStudentIds.set([]);
    this.clearMessages();
  }

  isDirty(): boolean {
    return this.sortedIds(this.draftStudentIds()) !== this.sortedIds(this.originalStudentIds());
  }

  saveAssignments(): void {
    const teacherId = this.selectedTeacherId();
    if (!teacherId || !this.isDirty()) {
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.assignmentService.save(teacherId, this.draftStudentIds()).subscribe({
      next: () => {
        this.saving.set(false);
        this.success.set(this.languageService.t('teacherAssignments.saved'));
        this.load(teacherId);
      },
      error: (response) => {
        this.saving.set(false);
        this.error.set(
          response?.error?.message ??
            response?.error?.Message ??
            this.languageService.t('teacherAssignments.saveError'),
        );
      },
    });
  }

  initials(name: string): string {
    return (
      name
        .trim()
        .split(/\s+/)
        .slice(-2)
        .map((part) => part[0]?.toUpperCase())
        .join('') || 'T'
    );
  }

  private matchesStudent(student: AssignmentStudent, query: string): boolean {
    if (!query) {
      return true;
    }
    return this.normalize(
      `${student.fullName} ${student.studentCode} ${student.gradeLevel ?? ''} ${student.homeroom ?? ''}`,
    ).includes(query);
  }

  private normalize(value: string): string {
    return value.trim().toLocaleLowerCase();
  }

  private sortedIds(ids: number[]): string {
    return [...ids].sort((a, b) => a - b).join(',');
  }

  private clearMessages(): void {
    this.error.set(null);
    this.success.set(null);
  }
}
