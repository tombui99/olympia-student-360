import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { blankQuestion, blankSection, CustomFormsService, FormQuestion, FormQuestionType, FormSection, newId, SaveCustomForm } from './custom-forms.service';
import { QuestionTypeIconComponent } from './question-type-icon.component';

@Component({
  selector: 'app-form-builder',
  imports: [CommonModule, FormsModule, RouterLink, QuestionTypeIconComponent],
  template: `
    <section class="mx-auto max-w-6xl space-y-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="flex items-center gap-3"><a routerLink="/forms" class="grid h-9 w-9 place-items-center rounded-full border border-[#ddd4e2] text-[#32124f]">←</a><div><p class="text-xs uppercase tracking-[.16em] text-[#8a6811]">{{ t('forms.builder') }}</p><h2 class="text-2xl font-semibold text-[#32124f]">{{ id ? t('forms.editForm') : t('forms.newForm') }}</h2></div></div>
        <div class="flex gap-2">
          <button class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] px-4 py-2 text-sm font-semibold text-[#32124f] transition hover:bg-[#f7f3fa] disabled:opacity-50" [disabled]="loadingForm()" (click)="openPreview()">
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/></svg>
            {{ t('forms.preview') }}
          </button>
          <button class="inline-flex items-center gap-2 rounded-md bg-[#32124f] px-5 py-2 text-sm font-semibold text-white transition hover:bg-[#4b2070] disabled:opacity-50" [disabled]="saving() || loadingForm()" (click)="save()">
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2Z"/><path d="M17 21v-8H7v8M7 3v5h8"/></svg>
            {{ saving() ? t('forms.saving') : t('forms.save') }}
          </button>
        </div>
      </div>
      @if (error()) { <div class="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{{ error() }}</div> }

      @if (loadingForm()) {
        <div class="grid min-h-72 place-items-center rounded-xl border border-[#e2dbe7] bg-white shadow-[0_10px_30px_rgba(50,18,79,.06)]">
          <div class="text-center"><span class="mx-auto block h-8 w-8 animate-spin rounded-full border-4 border-[#ded3e5] border-t-[#5b287c]"></span><p class="mt-3 text-sm font-medium text-[#746b7b]">{{ t('forms.loadingForm') }}</p></div>
        </div>
      }

      <div class="grid gap-5 xl:grid-cols-[minmax(0,1fr)_19rem]" [class.hidden]="loadingForm()">
        <main class="space-y-5">
          <article class="rounded-xl border-t-8 border-t-[#5b287c] bg-white p-6 shadow-[0_10px_30px_rgba(50,18,79,.08)]">
            <input class="w-full border-b border-transparent text-3xl font-semibold text-[#32124f] outline-none focus:border-[#6d3a91]" [(ngModel)]="model.title" [placeholder]="t('forms.formTitle')">
            <textarea class="mt-3 w-full resize-none border-b border-transparent text-sm text-[#6f6877] outline-none focus:border-[#6d3a91]" rows="2" [(ngModel)]="model.description" [placeholder]="t('forms.formDescription')"></textarea>
          </article>

          @for (section of model.definition.sections; track section.id; let sectionIndex = $index) {
            <section class="space-y-3 rounded-xl border border-[#e2d8e7] bg-[#f4eff7] p-3 sm:p-4">
              <div class="rounded-lg bg-[#32124f] p-4 text-white">
                <div class="flex gap-3"><span class="mt-1 grid h-6 w-6 shrink-0 place-items-center rounded-full bg-[#f3c246] text-xs font-bold text-[#32124f]">{{ sectionIndex + 1 }}</span><div class="min-w-0 flex-1"><input class="w-full border-b border-white/20 bg-transparent text-lg font-semibold outline-none focus:border-[#f3c246]" [(ngModel)]="section.title"><input class="mt-2 w-full border-b border-transparent bg-transparent text-sm text-white/70 outline-none focus:border-white/30" [(ngModel)]="section.description" [placeholder]="t('forms.sectionDescription')"></div><div class="flex items-start gap-1"><button class="px-2" [disabled]="sectionIndex===0" (click)="moveSection(sectionIndex,-1)">↑</button><button class="px-2" [disabled]="sectionIndex===model.definition.sections.length-1" (click)="moveSection(sectionIndex,1)">↓</button><button class="px-2 text-red-200" [disabled]="model.definition.sections.length===1" (click)="removeSection(sectionIndex)">×</button></div></div>
              </div>

              @for (question of section.questions; track question.id; let questionIndex = $index) {
                <article class="rounded-lg border-l-4 border-l-[#6d3a91] bg-white p-4 shadow-sm">
                  <div class="grid gap-3 md:grid-cols-[minmax(0,1fr)_14rem]">
                    <input class="border-b border-[#d8d0df] px-2 py-2 font-medium text-[#32124f] outline-none focus:border-[#6d3a91]" [(ngModel)]="question.label" [placeholder]="t('forms.question')">
                    <div class="relative">
                      <button
                        type="button"
                        class="flex w-full items-center gap-3 rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-left text-sm font-medium text-[#332b38] transition hover:bg-[#f7f5f8] focus:border-[#6d3a91] focus:outline-none focus:ring-2 focus:ring-[#6d3a91]/15"
                        [attr.aria-expanded]="openTypeQuestionId() === question.id"
                        aria-haspopup="listbox"
                        (click)="toggleTypeMenu(question.id, $event)"
                      >
                        <app-question-type-icon class="shrink-0 text-[#646970]" [type]="question.type" />
                        <span class="min-w-0 flex-1 truncate">{{ t(typeLabel(question.type)) }}</span>
                        <svg class="h-4 w-4 shrink-0 text-[#77717b] transition" [class.rotate-180]="openTypeQuestionId() === question.id" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path fill-rule="evenodd" d="M5.2 7.4a.75.75 0 0 1 1.05-.1L10 10.4l3.75-3.1a.75.75 0 1 1 .95 1.16l-4.23 3.5a.75.75 0 0 1-.95 0l-4.23-3.5a.75.75 0 0 1-.1-1.06Z" clip-rule="evenodd" /></svg>
                      </button>
                      @if (openTypeQuestionId() === question.id) {
                        <button class="fixed inset-0 z-40 cursor-default" type="button" tabindex="-1" aria-label="Close question type menu" (click)="openTypeQuestionId.set(null)"></button>
                        <div
                          class="fixed z-50 w-[16.5rem] overflow-y-auto overscroll-contain rounded-lg border border-[#dedbe1] bg-white py-1.5 shadow-[0_10px_30px_rgba(32,25,38,.22)]"
                          role="listbox"
                          [attr.aria-label]="t('forms.questionType')"
                          [style.left.px]="typeMenuPosition().left"
                          [style.top.px]="typeMenuPosition().top"
                          [style.bottom.px]="typeMenuPosition().bottom"
                          [style.max-height.px]="typeMenuPosition().maxHeight"
                        >
                          @for (type of questionTypes; track type.value; let typeIndex = $index) {
                            @if (typeIndex === 2 || typeIndex === 5 || typeIndex === 7 || typeIndex === 9) { <div class="my-1 h-px bg-[#e3e0e4]" role="separator"></div> }
                            <button
                              type="button"
                              role="option"
                              class="flex w-full items-center gap-3 px-4 py-2 text-left text-[13px] font-medium leading-5 text-[#29252c] transition hover:bg-[#f1f3f8] focus:bg-[#f1f3f8] focus:outline-none"
                              [class.bg-[#eaf1ff]]="question.type === type.value"
                              [attr.aria-selected]="question.type === type.value"
                              (click)="selectQuestionType(question, type.value)"
                            >
                              <app-question-type-icon class="shrink-0 text-[#646970] [&>svg]:h-5 [&>svg]:w-5" [type]="type.value" />
                              <span>{{ t(type.labelKey) }}</span>
                            </button>
                          }
                        </div>
                      }
                    </div>
                  </div>
                  <input class="mt-2 w-full border-b border-transparent px-2 py-1 text-sm text-[#746b7b] outline-none focus:border-[#d8d0df]" [(ngModel)]="question.description" [placeholder]="t('forms.questionDescription')">

                  @if (hasOptions(question)) {
                    <div class="mt-4 space-y-2">@for (option of question.options; track $index; let optionIndex=$index) { <div class="flex items-center gap-2"><span class="text-[#8b8193]">{{ question.type==='checkboxes' ? '□' : '○' }}</span><input class="flex-1 border-b border-[#e5dfe8] px-2 py-1 text-sm outline-none focus:border-[#6d3a91]" [(ngModel)]="question.options[optionIndex]"><button class="text-[#8b8193]" (click)="question.options.splice(optionIndex,1)">×</button></div> }<button class="ml-6 text-xs font-semibold text-[#6d3a91]" (click)="question.options.push('Option '+(question.options.length+1))">+ {{ t('forms.addOption') }}</button></div>
                  }
                  @if (isGrid(question)) {
                    <div class="mt-4 grid gap-4 sm:grid-cols-2"><div><p class="mb-2 text-xs font-bold uppercase text-[#746b7b]">{{ t('forms.rows') }}</p>@for (row of question.rows; track $index; let i=$index) { <div class="mb-2 flex gap-2"><input class="min-w-0 flex-1 border-b px-2 py-1 text-sm" [(ngModel)]="question.rows[i]"><button (click)="question.rows.splice(i,1)">×</button></div> }<button class="text-xs font-semibold text-[#6d3a91]" (click)="question.rows.push('Row '+(question.rows.length+1))">+ {{ t('forms.addRow') }}</button></div><div><p class="mb-2 text-xs font-bold uppercase text-[#746b7b]">{{ t('forms.columns') }}</p>@for (column of question.columns; track $index; let i=$index) { <div class="mb-2 flex gap-2"><input class="min-w-0 flex-1 border-b px-2 py-1 text-sm" [(ngModel)]="question.columns[i]"><button (click)="question.columns.splice(i,1)">×</button></div> }<button class="text-xs font-semibold text-[#6d3a91]" (click)="question.columns.push('Column '+(question.columns.length+1))">+ {{ t('forms.addColumn') }}</button></div></div>
                  }
                  @if (question.type==='linearScale' || question.type==='rating') { <div class="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4"><label class="text-xs text-[#746b7b]">{{ t('forms.minimum') }}<input type="number" class="mt-1 w-full rounded border px-2 py-2" [(ngModel)]="question.min"></label><label class="text-xs text-[#746b7b]">{{ t('forms.maximum') }}<input type="number" class="mt-1 w-full rounded border px-2 py-2" [(ngModel)]="question.max"></label><label class="text-xs text-[#746b7b]">{{ t('forms.minLabel') }}<input class="mt-1 w-full rounded border px-2 py-2" [(ngModel)]="question.minLabel"></label><label class="text-xs text-[#746b7b]">{{ t('forms.maxLabel') }}<input class="mt-1 w-full rounded border px-2 py-2" [(ngModel)]="question.maxLabel"></label></div> }

                  <details class="mt-4 rounded-md bg-[#faf8fb] p-3"><summary class="cursor-pointer text-xs font-semibold text-[#5b287c]">{{ t('forms.validationAndBranching') }}</summary><div class="mt-3 grid gap-3 sm:grid-cols-2"><label class="text-xs text-[#746b7b]">{{ t('forms.validationPattern') }}<input class="mt-1 w-full rounded border px-2 py-2" [(ngModel)]="question.validationPattern" placeholder="Regular expression"></label><label class="text-xs text-[#746b7b]">{{ t('forms.validationMessage') }}<input class="mt-1 w-full rounded border px-2 py-2" [(ngModel)]="question.validationMessage"></label></div>
                    @if (canBranch(question) && model.definition.sections.length > 1) { <div class="mt-4"><p class="mb-2 text-xs font-semibold text-[#5b287c]">{{ t('forms.branching') }}</p>@for (option of question.options; track $index) { <label class="mb-2 grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] items-center gap-3 text-xs"><span>{{ option }}</span><select class="rounded border bg-white px-2 py-2" [ngModel]="branchTarget(question,option)" (ngModelChange)="setBranch(question,option,$event)"><option value="">{{ t('forms.continueNext') }}</option>@for (target of model.definition.sections; track target.id) { @if (target.id !== section.id) { <option [value]="target.id">{{ target.title }}</option> } }<option value="__submit">{{ t('forms.submitForm') }}</option></select></label> }</div> }
                  </details>
                  <div class="mt-4 flex flex-wrap items-center justify-end gap-2 border-t border-[#eee8f1] pt-3"><button class="rounded px-2 py-1 text-xs" [disabled]="questionIndex===0" (click)="moveQuestion(section,questionIndex,-1)">↑</button><button class="rounded px-2 py-1 text-xs" [disabled]="questionIndex===section.questions.length-1" (click)="moveQuestion(section,questionIndex,1)">↓</button><button class="rounded px-2 py-1 text-xs font-semibold text-[#5b287c]" (click)="duplicateQuestion(section,questionIndex)">{{ t('forms.duplicate') }}</button><button class="rounded px-2 py-1 text-xs font-semibold text-red-700" (click)="section.questions.splice(questionIndex,1)">{{ t('forms.delete') }}</button><label class="ml-2 flex items-center gap-2 text-xs font-semibold"><input type="checkbox" [(ngModel)]="question.required"> {{ t('forms.required') }}</label></div>
                </article>
              }
              <button class="w-full rounded-lg border-2 border-dashed border-[#cdbed5] bg-white/60 py-3 text-sm font-semibold text-[#5b287c] hover:bg-white" (click)="addQuestion(section)">+ {{ t('forms.addQuestion') }}</button>
            </section>
          }
          <button class="w-full rounded-lg border-2 border-dashed border-[#a88db8] py-4 text-sm font-semibold text-[#5b287c]" (click)="model.definition.sections.push(blankSection())">+ {{ t('forms.addSection') }}</button>
        </main>

        <aside class="space-y-4 xl:sticky xl:top-5 xl:self-start">
          <section class="rounded-xl border border-[#e2dbe7] bg-white p-4"><h3 class="font-semibold text-[#32124f]">{{ t('forms.settings') }}</h3><div class="mt-4 space-y-4"><label class="flex items-center justify-between gap-3 text-sm"><span>{{ t('forms.allowMultiple') }}</span><input type="checkbox" [(ngModel)]="model.allowMultipleSubmissions"></label><label class="block text-xs text-[#746b7b]">{{ t('forms.opensAt') }}<input type="datetime-local" class="mt-1 w-full rounded-md border px-3 py-2 text-sm" [(ngModel)]="opensAt"></label><label class="block text-xs text-[#746b7b]">{{ t('forms.closesAt') }}<input type="datetime-local" class="mt-1 w-full rounded-md border px-3 py-2 text-sm" [(ngModel)]="closesAt"></label><label class="block text-xs text-[#746b7b]">{{ t('forms.responseLimit') }}<input type="number" min="1" class="mt-1 w-full rounded-md border px-3 py-2 text-sm" [(ngModel)]="model.responseLimit" [placeholder]="t('forms.unlimited')"></label><label class="block text-xs text-[#746b7b]">{{ t('forms.confirmation') }}<textarea rows="3" class="mt-1 w-full rounded-md border px-3 py-2 text-sm" [(ngModel)]="model.confirmationMessage"></textarea></label></div></section>
          @if (id) {
            <div class="space-y-2 rounded-xl border border-[#e2dbe7] bg-white p-4">
              <button class="inline-flex w-full items-center justify-center gap-2 rounded-md border border-[#d8d0df] px-4 py-2.5 text-sm font-semibold text-[#51256e] transition hover:bg-[#f7f3fa] disabled:opacity-50" [disabled]="deleting()" (click)="archive()">
                <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 8v13H3V8"/><path d="M1 3h22v5H1zM10 12h4"/></svg>
                {{ t('forms.archive') }}
              </button>
              <button class="inline-flex w-full items-center justify-center gap-2 rounded-md border border-red-200 px-4 py-2.5 text-sm font-semibold text-red-700 transition hover:border-red-300 hover:bg-red-50 disabled:cursor-wait disabled:opacity-50" [disabled]="deleting()" (click)="deletePermanently()">
                <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 6h18"/><path d="M8 6V4h8v2M19 6l-1 14H6L5 6M10 11v5M14 11v5"/></svg>
                {{ t('forms.deleteForm') }}
              </button>
            </div>
          }
        </aside>
      </div>
    </section>

    @if (preview()) {
      <div class="fixed inset-0 z-[100] overflow-y-auto bg-[#eeeaf7] px-4 py-6 text-[#24182f]" (click)="preview.set(false)">
        <div class="mx-auto max-w-3xl" (click)="$event.stopPropagation()">
          <header class="mb-5 flex items-center justify-between gap-4">
            <div class="flex items-center gap-3"><img src="/assets/olympia_logo.png" class="h-11 w-11 object-contain" alt="Olympia"><div><p class="text-xs uppercase tracking-[.15em] text-[#8a6811]">{{ t('forms.previewMode') }}</p><p class="font-semibold text-[#32124f]">Student 360</p></div></div>
            <button class="rounded-full bg-white px-4 py-2 text-sm font-semibold text-[#32124f] shadow" (click)="preview.set(false)">× {{ t('forms.closePreview') }}</button>
          </header>

          @if (previewSubmitted()) {
            <article class="rounded-xl border-t-8 border-emerald-600 bg-white p-8 shadow"><div class="mb-4 grid h-12 w-12 place-items-center rounded-full bg-emerald-100 text-2xl text-emerald-700">✓</div><h1 class="text-2xl font-semibold text-[#32124f]">{{ t('publicForm.complete') }}</h1><p class="mt-3 text-[#746b7b]">{{ model.confirmationMessage }}</p><button class="mt-6 rounded-md border border-[#d4cbd9] px-4 py-2 text-sm font-semibold" (click)="restartPreview()">{{ t('forms.restartPreview') }}</button></article>
          } @else {
            <article class="rounded-xl border-t-8 border-[#5b287c] bg-white p-6 shadow sm:p-8"><h1 class="text-3xl font-semibold text-[#32124f]">{{ model.title || t('forms.formTitle') }}</h1><p class="mt-2 text-[#746b7b]">{{ model.description }}</p><div class="mt-5 rounded-md bg-[#fff8e1] p-3 text-sm"><span class="font-semibold">{{ t('forms.previewStudent') }}</span> · STU-0001 · Grade 8 / 8A</div></article>

            @if (previewCurrentSection(); as section) {
              <div class="mt-4 rounded-t-xl bg-[#32124f] px-6 py-4 text-white"><p class="text-xs font-semibold uppercase tracking-[.12em] text-[#f3c246]">{{ t('publicForm.section') }} {{ previewSectionIndex()+1 }} / {{ model.definition.sections.length }}</p><h2 class="mt-1 text-xl font-semibold">{{ section.title }}</h2><p class="mt-1 text-sm text-white/70">{{ section.description }}</p></div>
              @for (q of section.questions; track q.id) {
                <article class="mt-3 rounded-xl bg-white p-5 shadow-sm sm:p-6"><p class="font-semibold text-[#32124f]">{{ q.label }} @if(q.required){<span class="text-red-600">*</span>}</p><p class="mt-1 text-sm text-[#746b7b]">{{ q.description }}</p><div class="mt-4">
                  @if (q.type==='shortText') { <input class="w-full border-b border-[#bfb4c5] px-1 py-2 outline-none focus:border-[#6d3a91]" [(ngModel)]="previewAnswers[q.id]"> }
                  @if (q.type==='paragraph') { <textarea rows="4" class="w-full rounded-md border border-[#d4cbd9] p-3 outline-none focus:border-[#6d3a91]" [(ngModel)]="previewAnswers[q.id]"></textarea> }
                  @if (q.type==='date') { <input type="date" class="rounded-md border border-[#d4cbd9] px-3 py-2" [(ngModel)]="previewAnswers[q.id]"> }
                  @if (q.type==='time') { <input type="time" class="rounded-md border border-[#d4cbd9] px-3 py-2" [(ngModel)]="previewAnswers[q.id]"> }
                  @if (q.type==='dropdown') { <select class="w-full rounded-md border border-[#d4cbd9] bg-white px-3 py-2.5" [(ngModel)]="previewAnswers[q.id]"><option value="">{{ t('publicForm.choose') }}</option>@for(option of q.options;track $index){<option [value]="option">{{ option }}</option>}</select> }
                  @if (q.type==='multipleChoice') { <div class="space-y-3">@for(option of q.options;track $index){<label class="flex items-center gap-3"><input type="radio" [name]="'preview_'+q.id" [value]="option" [(ngModel)]="previewAnswers[q.id]"> <span>{{ option }}</span></label>}</div> }
                  @if (q.type==='checkboxes') { <div class="space-y-3">@for(option of q.options;track $index){<label class="flex items-center gap-3"><input type="checkbox" [checked]="previewChecked(q.id,option)" (change)="togglePreviewCheck(q.id,option,$event)"> <span>{{ option }}</span></label>}</div> }
                  @if (q.type==='linearScale' || q.type==='rating') { <div class="flex flex-wrap items-end gap-3"><span class="text-xs text-[#746b7b]">{{ q.minLabel }}</span>@for(value of previewScale(q);track value){<label class="grid gap-1 text-center text-sm"><span [class.text-amber-500]="q.type==='rating'">{{ q.type==='rating' ? '★' : value }}</span><input type="radio" [name]="'preview_'+q.id" [value]="value" [(ngModel)]="previewAnswers[q.id]"></label>}<span class="text-xs text-[#746b7b]">{{ q.maxLabel }}</span></div> }
                  @if (q.type==='multipleChoiceGrid' || q.type==='checkboxGrid') { <div class="overflow-x-auto"><table class="min-w-full text-sm"><thead><tr><th></th>@for(column of q.columns;track $index){<th class="min-w-28 px-2 py-2 text-center font-medium text-[#746b7b]">{{ column }}</th>}</tr></thead><tbody>@for(row of q.rows;track $index){<tr class="odd:bg-[#f8f5fa]"><th class="px-3 py-3 text-left font-medium">{{ row }}</th>@for(column of q.columns;track $index){<td class="px-2 py-3 text-center">@if(q.type==='multipleChoiceGrid'){<input type="radio" [name]="'preview_'+q.id+'_'+row" [checked]="previewGridValue(q.id,row)===column" (change)="setPreviewGridRadio(q.id,row,column)">}@else{<input type="checkbox" [checked]="previewGridChecked(q.id,row,column)" (change)="togglePreviewGridCheck(q.id,row,column,$event)">}</td>}</tr>}</tbody></table></div> }
                </div></article>
              }
              <div class="mt-5 flex justify-between gap-3">@if(previewSectionHistory.length){<button class="rounded-md border border-[#cfc5d5] bg-white px-5 py-2.5 font-semibold text-[#32124f]" (click)="previewBack()">{{ t('publicForm.back') }}</button>}@else{<span></span>}<button class="rounded-md bg-[#32124f] px-6 py-2.5 font-semibold text-white" (click)="previewNext()">{{ previewIsLastStep() ? t('publicForm.submit') : t('publicForm.next') }}</button></div>
            }
          }
        </div>
      </div>
    }
  `,
})
export class FormBuilderComponent implements OnInit {
  id: number | null = null; saving = signal(false); error = signal<string | null>(null); preview = signal(false); opensAt = ''; closesAt = '';
  loadingForm = signal(false);
  deleting = signal(false);
  previewSectionIndex = signal(0);
  previewSubmitted = signal(false);
  previewSectionHistory:number[] = [];
  previewAnswers:Record<string,any> = {};
  openTypeQuestionId = signal<string | null>(null);
  typeMenuPosition = signal<{left:number;top:number|null;bottom:number|null;maxHeight:number}>({left:0,top:0,bottom:null,maxHeight:480});
  readonly blankSection = blankSection;
  model: SaveCustomForm = { title: '', description: '', confirmationMessage: 'Thank you. Your response has been recorded.', allowMultipleSubmissions: true, opensAt: null, closesAt: null, responseLimit: null, definition: { sections: [blankSection()] } };
  questionTypes: { value: FormQuestionType; labelKey: string }[] = [
    {value:'shortText',labelKey:'forms.types.shortText'},{value:'paragraph',labelKey:'forms.types.paragraph'},{value:'multipleChoice',labelKey:'forms.types.multipleChoice'},{value:'checkboxes',labelKey:'forms.types.checkboxes'},{value:'dropdown',labelKey:'forms.types.dropdown'},{value:'linearScale',labelKey:'forms.types.linearScale'},{value:'rating',labelKey:'forms.types.rating'},{value:'multipleChoiceGrid',labelKey:'forms.types.multipleChoiceGrid'},{value:'checkboxGrid',labelKey:'forms.types.checkboxGrid'},{value:'date',labelKey:'forms.types.date'},{value:'time',labelKey:'forms.types.time'},
  ];
  constructor(private route: ActivatedRoute, private router: Router, private service: CustomFormsService, private language: LanguageService, private toast: ToastService) {}
  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      const value = params.get('id');
      if (!value || value === 'new') { this.id = null; this.loadingForm.set(false); return; }
      const formId = Number(value);
      if (!Number.isInteger(formId) || formId < 1) { this.error.set(this.t('forms.loadError')); return; }
      this.id = formId;
      this.error.set(null);
      this.loadingForm.set(true);
      this.service.detail(formId).subscribe({
        next: detail => {
          if (this.id !== formId) return;
          this.model = { title: detail.title, description: detail.description, confirmationMessage: detail.confirmationMessage, allowMultipleSubmissions: detail.allowMultipleSubmissions, opensAt: detail.opensAt, closesAt: detail.closesAt, responseLimit: detail.responseLimit, definition: detail.definition };
          this.opensAt = this.localDate(detail.opensAt);
          this.closesAt = this.localDate(detail.closesAt);
          this.loadingForm.set(false);
        },
        error: () => {
          if (this.id !== formId) return;
          this.error.set(this.t('forms.loadError'));
          this.loadingForm.set(false);
        },
      });
    });
  }
  t(key: string) { return this.language.t(key); }
  typeLabel(type: FormQuestionType) { return this.questionTypes.find(item => item.value === type)?.labelKey || type; }
  hasOptions(q: FormQuestion) { return ['multipleChoice','checkboxes','dropdown'].includes(q.type); }
  isGrid(q: FormQuestion) { return ['multipleChoiceGrid','checkboxGrid'].includes(q.type); }
  canBranch(q: FormQuestion) { return ['multipleChoice','dropdown'].includes(q.type); }
  typeChanged(q: FormQuestion) { if (this.hasOptions(q) && !q.options.length) q.options=['Option 1']; if (this.isGrid(q)) { if(!q.rows.length)q.rows=['Row 1']; if(!q.columns.length)q.columns=['Column 1']; } if (!this.canBranch(q)) q.branches=[]; }
  toggleTypeMenu(questionId:string,event:MouseEvent){
    if(this.openTypeQuestionId()===questionId){this.openTypeQuestionId.set(null);return;}
    const trigger=event.currentTarget as HTMLElement;
    const rect=trigger.getBoundingClientRect();
    const viewportWidth=window.innerWidth;
    const viewportHeight=window.innerHeight;
    const menuWidth=264;
    const idealMenuHeight=474;
    const gap=6;
    const edge=12;
    const availableBelow=viewportHeight-rect.bottom-gap-edge;
    const availableAbove=rect.top-gap-edge;
    const openAbove=availableBelow<Math.min(idealMenuHeight,availableAbove);
    const left=Math.max(edge,Math.min(rect.right-menuWidth,viewportWidth-menuWidth-edge));
    this.typeMenuPosition.set(openAbove
      ? {left,top:null,bottom:viewportHeight-rect.top+gap,maxHeight:Math.max(180,availableAbove)}
      : {left,top:rect.bottom+gap,bottom:null,maxHeight:Math.max(180,availableBelow)});
    this.openTypeQuestionId.set(questionId);
  }
  selectQuestionType(question:FormQuestion,type:FormQuestionType){question.type=type;this.typeChanged(question);this.openTypeQuestionId.set(null);}
  @HostListener('document:keydown.escape') closeTypeMenu(){this.openTypeQuestionId.set(null);}
  @HostListener('window:resize') closeTypeMenuOnResize(){this.openTypeQuestionId.set(null);}
  @HostListener('window:scroll') closeTypeMenuOnScroll(){this.openTypeQuestionId.set(null);}
  addQuestion(section: FormSection) { section.questions.push(blankQuestion()); }
  duplicateQuestion(section: FormSection,index:number) { const copy=structuredClone(section.questions[index]); copy.id=newId('q'); section.questions.splice(index+1,0,copy); }
  moveQuestion(section:FormSection,index:number,delta:number) { const target=index+delta; if(target<0||target>=section.questions.length)return; [section.questions[index],section.questions[target]]=[section.questions[target],section.questions[index]]; }
  moveSection(index:number,delta:number) { const target=index+delta; if(target<0||target>=this.model.definition.sections.length)return; [this.model.definition.sections[index],this.model.definition.sections[target]]=[this.model.definition.sections[target],this.model.definition.sections[index]]; }
  removeSection(index:number) { const removed=this.model.definition.sections[index].id; this.model.definition.sections.splice(index,1); for(const q of this.allQuestions())q.branches=q.branches.filter(b=>b.targetSectionId!==removed); }
  branchTarget(q:FormQuestion,option:string){return q.branches.find(b=>b.value===option)?.targetSectionId||'';}
  setBranch(q:FormQuestion,option:string,target:string){q.branches=q.branches.filter(b=>b.value!==option);if(target)q.branches.push({value:option,targetSectionId:target});}
  allQuestions(){return this.model.definition.sections.flatMap(s=>s.questions);}
  openPreview(){this.restartPreview();this.preview.set(true);}
  restartPreview(){this.previewSectionIndex.set(0);this.previewSubmitted.set(false);this.previewSectionHistory=[];this.previewAnswers={};}
  previewCurrentSection(){return this.model.definition.sections[this.previewSectionIndex()]||null;}
  previewChecked(id:string,option:string){return Array.isArray(this.previewAnswers[id])&&this.previewAnswers[id].includes(option);}
  togglePreviewCheck(id:string,option:string,event:Event){const checked=(event.target as HTMLInputElement).checked;const values=Array.isArray(this.previewAnswers[id])?[...this.previewAnswers[id]]:[];this.previewAnswers[id]=checked?[...values,option]:values.filter((value:string)=>value!==option);}
  previewScale(question:FormQuestion){const min=question.min??1,max=question.max??5;return Array.from({length:Math.max(1,max-min+1)},(_,index)=>min+index);}
  previewGrid(id:string):Record<string,any>{if(!this.previewAnswers[id]||typeof this.previewAnswers[id]!=='object')this.previewAnswers[id]={};return this.previewAnswers[id];}
  previewGridValue(id:string,row:string){return this.previewGrid(id)[row];}
  setPreviewGridRadio(id:string,row:string,column:string){this.previewGrid(id)[row]=column;}
  previewGridChecked(id:string,row:string,column:string){return Array.isArray(this.previewGrid(id)[row])&&this.previewGrid(id)[row].includes(column);}
  togglePreviewGridCheck(id:string,row:string,column:string,event:Event){const checked=(event.target as HTMLInputElement).checked;const values=Array.isArray(this.previewGrid(id)[row])?[...this.previewGrid(id)[row]]:[];this.previewGrid(id)[row]=checked?[...values,column]:values.filter((value:string)=>value!==column);}
  previewBranchTarget(section:FormSection){let target:string|undefined;for(const question of section.questions.filter(item=>['multipleChoice','dropdown'].includes(item.type)))target=question.branches.find(branch=>branch.value===this.previewAnswers[question.id])?.targetSectionId||target;return target;}
  previewNext(){const section=this.previewCurrentSection();if(!section)return;const target=this.previewBranchTarget(section);if(target==='__submit'){this.previewSubmitted.set(true);return;}const nextIndex=target?this.model.definition.sections.findIndex(item=>item.id===target):this.previewSectionIndex()+1;if(nextIndex<0||nextIndex>=this.model.definition.sections.length){this.previewSubmitted.set(true);return;}this.previewSectionHistory.push(this.previewSectionIndex());this.previewSectionIndex.set(nextIndex);}
  previewBack(){const index=this.previewSectionHistory.pop();if(index!=null)this.previewSectionIndex.set(index);}
  previewIsLastStep(){const section=this.previewCurrentSection();if(!section)return true;const target=this.previewBranchTarget(section);return target==='__submit'||(!target&&this.previewSectionIndex()===this.model.definition.sections.length-1);}
  save(){if(this.loadingForm())return;this.error.set(null);this.saving.set(true);this.model.opensAt=this.opensAt?new Date(this.opensAt).toISOString():null;this.model.closesAt=this.closesAt?new Date(this.closesAt).toISOString():null;const request=this.id?this.service.update(this.id,this.model):this.service.create(this.model);request.subscribe({next:()=>this.router.navigate(['/forms'],{replaceUrl:true}),error:e=>{this.error.set(e.error?.message||this.t('forms.saveError'));this.saving.set(false);}});}
  archive(){if(!this.id||!confirm(this.t('forms.confirmArchive')))return;this.service.archive(this.id).subscribe(()=>this.router.navigate(['/forms']));}
  deletePermanently(){
    if(!this.id||this.deleting()||!confirm(this.t('forms.confirmDeleteForm')))return;
    this.deleting.set(true);
    this.service.deletePermanently(this.id).subscribe({
      next:()=>{this.toast.success(this.t('forms.formDeleted'));this.router.navigate(['/forms'],{replaceUrl:true});},
      error:()=>{this.deleting.set(false);this.error.set(this.t('forms.deleteFormError'));},
    });
  }
  localDate(value?:string|null){if(!value)return '';const date=new Date(value);const offset=date.getTimezoneOffset();return new Date(date.getTime()-offset*60000).toISOString().slice(0,16);}
}
