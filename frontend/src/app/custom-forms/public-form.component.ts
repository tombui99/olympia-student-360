import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { CustomFormsService, FormQuestion, FormSection, FormStudentCandidate, VerifiedStudent } from './custom-forms.service';

@Component({
  selector: 'app-public-form',
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-[#eeeaf7] px-4 py-7 text-[#24182f] sm:py-10">
      <header class="mx-auto mb-5 flex max-w-3xl items-center justify-between">
        <div class="flex items-center gap-3"><img src="/assets/olympia_logo.png" class="h-11 w-11 object-contain" alt="Olympia"><div><p class="text-xs uppercase tracking-[.15em] text-[#8a6811]">The Olympia Schools</p><p class="font-semibold text-[#32124f]">Student 360</p></div></div>
        <div class="rounded-full bg-white p-1 shadow"><button class="rounded-full px-3 py-1.5 text-xs font-bold" [class.bg-[#32124f]]="language.language()==='en'" [class.text-white]="language.language()==='en'" (click)="language.use('en')">EN</button><button class="rounded-full px-3 py-1.5 text-xs font-bold" [class.bg-[#32124f]]="language.language()==='vi'" [class.text-white]="language.language()==='vi'" (click)="language.use('vi')">VI</button></div>
      </header>
      <main class="mx-auto max-w-3xl">
        @if (loading()) { <div class="rounded-xl bg-white p-10 text-center shadow">{{ t('common.loading') }}</div> }
        @else if (notFound()) { <div class="rounded-xl border-t-8 border-[#8f2f3d] bg-white p-8 shadow"><h1 class="text-2xl font-semibold text-[#32124f]">{{ t('publicForm.unavailable') }}</h1><p class="mt-2 text-[#746b7b]">{{ t('publicForm.unavailableHint') }}</p></div> }
        @else if (submitted()) { <div class="rounded-xl border-t-8 border-emerald-600 bg-white p-8 shadow"><div class="mb-4 grid h-12 w-12 place-items-center rounded-full bg-emerald-100 text-2xl text-emerald-700">✓</div><h1 class="text-2xl font-semibold text-[#32124f]">{{ t('publicForm.complete') }}</h1><p class="mt-3 whitespace-pre-wrap text-[#746b7b]">{{ successMessage() }}</p></div> }
        @else if (!verified()) {
          <article class="rounded-xl border-t-8 border-[#5b287c] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,.12)] sm:p-8"><h1 class="text-3xl font-semibold text-[#32124f]">{{ summaryTitle() }}</h1><p class="mt-2 text-[#746b7b]">{{ summaryDescription() }}</p><div class="my-6 h-px bg-[#eee8f1]"></div><h2 class="text-lg font-semibold text-[#32124f]">{{ t('publicForm.findStudent') }}</h2><p class="mt-1 text-sm text-[#746b7b]">{{ t('publicForm.verifyHint') }}</p>
            @if (error()) { <p class="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700">{{ error() }}</p> }
            <form class="mt-5 space-y-4" (ngSubmit)="verifyStudent()">
              <label class="block text-sm font-semibold text-[#4c4054]">{{ t('publicForm.studentId') }} <span class="font-normal text-[#817688]">({{ t('publicForm.optionalWithContact') }})</span><input class="mt-1.5 w-full rounded-md border border-[#d4cbd9] px-4 py-3 font-normal outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/15" name="studentId" [(ngModel)]="studentId" autocomplete="off"></label>
              <div class="flex items-center gap-3 text-xs font-semibold uppercase tracking-wider text-[#918697]"><span class="h-px flex-1 bg-[#e4dde8]"></span>{{ t('publicForm.orFindByContact') }}<span class="h-px flex-1 bg-[#e4dde8]"></span></div>
              <div class="grid gap-4 sm:grid-cols-2 sm:items-end">
                <label class="block text-sm font-semibold text-[#4c4054]">{{ t('publicForm.lookupEmail') }}<input type="email" class="mt-1.5 w-full rounded-md border border-[#d4cbd9] px-4 py-3 font-normal outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/15" name="studentEmail" [(ngModel)]="studentEmail" autocomplete="email"></label>
                <label class="block text-sm font-semibold text-[#4c4054]">{{ t('publicForm.lookupPhone') }}<input type="tel" class="mt-1.5 w-full rounded-md border border-[#d4cbd9] px-4 py-3 font-normal outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/15" name="studentPhone" [(ngModel)]="studentPhone" autocomplete="tel"></label>
              </div>
              <p class="text-xs text-[#817688]">{{ t('publicForm.contactLookupHint') }}</p>
              @if (candidates().length) {
                <div class="rounded-lg border border-[#ded4e3] bg-[#faf8fb] p-3"><p class="mb-2 text-sm font-semibold text-[#32124f]">{{ t('publicForm.selectStudent') }}</p><div class="space-y-2">@for (candidate of candidates(); track candidate.studentId) {<button type="button" class="flex w-full items-center justify-between rounded-md border border-[#ded4e3] bg-white px-4 py-3 text-left hover:border-[#6d3a91]" (click)="selectStudent(candidate)"><span><span class="block font-semibold text-[#32124f]">{{ candidate.studentName }}</span><span class="text-xs text-[#746b7b]">{{ candidate.studentId }} · {{ candidate.gradeLevel || '—' }} / {{ candidate.homeroom || '—' }}</span></span><span class="text-[#6d3a91]">›</span></button>}</div></div>
              }
              <button class="w-full rounded-md bg-[#32124f] px-5 py-3 font-semibold text-white disabled:opacity-50" [disabled]="working() || !canLookup()">{{ working() ? t('publicForm.verifying') : t('publicForm.continue') }}</button>
            </form>
          </article>
        } @else {
          <article class="rounded-xl border-t-8 border-[#5b287c] bg-white p-6 shadow sm:p-8"><h1 class="text-3xl font-semibold text-[#32124f]">{{ verified()!.form.title }}</h1><p class="mt-2 text-[#746b7b]">{{ verified()!.form.description }}</p><div class="mt-5 rounded-md bg-[#fff8e1] p-3 text-sm"><span class="font-semibold">{{ verified()!.studentName }}</span> · {{ verified()!.studentId }} · {{ verified()!.gradeLevel || '—' }} / {{ verified()!.homeroom || '—' }}</div></article>
          @if (currentSection(); as section) {
            <div class="mt-4 rounded-t-xl bg-[#32124f] px-6 py-4 text-white"><p class="text-xs font-semibold uppercase tracking-[.12em] text-[#f3c246]">{{ t('publicForm.section') }} {{ currentIndex()+1 }} / {{ verified()!.form.definition.sections.length }}</p><h2 class="mt-1 text-xl font-semibold">{{ section.title }}</h2><p class="mt-1 text-sm text-white/70">{{ section.description }}</p></div>
            @for (q of section.questions; track q.id) { <article class="mt-3 rounded-xl bg-white p-5 shadow-sm sm:p-6"><p class="font-semibold text-[#32124f]">{{ q.label }} @if(q.required){<span class="text-red-600">*</span>}</p><p class="mt-1 text-sm text-[#746b7b]">{{ q.description }}</p>
              <div class="mt-4">
                @if (q.type==='shortText') { <input class="w-full border-b border-[#bfb4c5] px-1 py-2 outline-none focus:border-[#6d3a91]" [(ngModel)]="answers[q.id]"> }
                @if (q.type==='paragraph') { <textarea rows="4" class="w-full rounded-md border border-[#d4cbd9] p-3 outline-none focus:border-[#6d3a91]" [(ngModel)]="answers[q.id]"></textarea> }
                @if (q.type==='date') { <input type="date" class="rounded-md border border-[#d4cbd9] px-3 py-2" [(ngModel)]="answers[q.id]"> }
                @if (q.type==='time') { <input type="time" class="rounded-md border border-[#d4cbd9] px-3 py-2" [(ngModel)]="answers[q.id]"> }
                @if (q.type==='dropdown') { <select class="w-full rounded-md border border-[#d4cbd9] bg-white px-3 py-2.5" [(ngModel)]="answers[q.id]"><option value="">{{ t('publicForm.choose') }}</option>@for(option of q.options;track $index){<option [value]="option">{{ option }}</option>}</select> }
                @if (q.type==='multipleChoice') { <div class="space-y-3">@for(option of q.options;track $index){<label class="flex items-center gap-3"><input type="radio" [name]="q.id" [value]="option" [(ngModel)]="answers[q.id]"> <span>{{ option }}</span></label>}</div> }
                @if (q.type==='checkboxes') { <div class="space-y-3">@for(option of q.options;track $index){<label class="flex items-center gap-3"><input type="checkbox" [checked]="isChecked(q.id,option)" (change)="toggleCheck(q.id,option,$event)"> <span>{{ option }}</span></label>}</div> }
                @if (q.type==='linearScale' || q.type==='rating') { <div class="flex flex-wrap items-end gap-3"><span class="text-xs text-[#746b7b]">{{ q.minLabel }}</span>@for(value of scale(q);track value){<label class="grid gap-1 text-center text-sm"><span>{{ q.type==='rating' ? '★' : value }}</span><input type="radio" [name]="q.id" [value]="value" [(ngModel)]="answers[q.id]"></label>}<span class="text-xs text-[#746b7b]">{{ q.maxLabel }}</span></div> }
                @if (q.type==='multipleChoiceGrid' || q.type==='checkboxGrid') { <div class="overflow-x-auto"><table class="min-w-full text-sm"><thead><tr><th></th>@for(column of q.columns;track $index){<th class="min-w-28 px-2 py-2 text-center font-medium text-[#746b7b]">{{ column }}</th>}</tr></thead><tbody>@for(row of q.rows;track $index){<tr class="odd:bg-[#f8f5fa]"><th class="px-3 py-3 text-left font-medium">{{ row }}</th>@for(column of q.columns;track $index){<td class="px-2 py-3 text-center">@if(q.type==='multipleChoiceGrid'){<input type="radio" [name]="q.id+'_'+row" [checked]="gridValue(q.id,row)===column" (change)="setGridRadio(q.id,row,column)">}@else{<input type="checkbox" [checked]="gridChecked(q.id,row,column)" (change)="toggleGridCheck(q.id,row,column,$event)">}</td>}</tr>}</tbody></table></div> }
              </div>
            </article> }
            @if (error()) { <p class="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700">{{ error() }}</p> }
            <div class="mt-5 flex justify-between gap-3">@if(sectionHistory.length){<button class="rounded-md border border-[#cfc5d5] bg-white px-5 py-2.5 font-semibold text-[#32124f]" (click)="back()">{{ t('publicForm.back') }}</button>}@else{<span></span>}<button class="rounded-md bg-[#32124f] px-6 py-2.5 font-semibold text-white disabled:opacity-50" [disabled]="working()" (click)="next()">{{ isLastStep() ? t('publicForm.submit') : t('publicForm.next') }}</button></div>
          }
        }
      </main>
    </div>
  `,
})
export class PublicFormComponent implements OnInit {
  publicId=''; loading=signal(true); notFound=signal(false); working=signal(false); submitted=signal(false); error=signal<string|null>(null); successMessage=signal('');
  summaryTitle=signal(''); summaryDescription=signal<string|null>(null); verified=signal<VerifiedStudent|null>(null); candidates=signal<FormStudentCandidate[]>([]); currentIndex=signal(0); sectionHistory:number[]=[];
  studentId='';studentEmail='';studentPhone='';answers:Record<string,any>={};
  constructor(private route:ActivatedRoute,private service:CustomFormsService,readonly language:LanguageService){}
  ngOnInit(){this.publicId=this.route.snapshot.paramMap.get('publicId')||'';this.service.publicSummary(this.publicId).subscribe({next:summary=>{this.summaryTitle.set(summary.title);this.summaryDescription.set(summary.description||null);this.loading.set(false);},error:()=>{this.notFound.set(true);this.loading.set(false);}});}
  t(key:string){return this.language.t(key);}
  canLookup(){return !!(this.studentId.trim()||this.studentEmail.trim()||this.studentPhone.trim());}
  verifyStudent(){if(!this.canLookup())return;this.error.set(null);this.candidates.set([]);if(this.studentId.trim()){this.verifyById();return;}this.working.set(true);this.service.lookupStudents(this.publicId,this.studentEmail,this.studentPhone).subscribe({next:matches=>{this.working.set(false);if(matches.length===1){this.selectStudent(matches[0]);return;}this.candidates.set(matches);},error:()=>{this.error.set(this.t('publicForm.contactVerifyError'));this.working.set(false);}});}
  selectStudent(candidate:FormStudentCandidate){this.studentId=candidate.studentId;this.candidates.set([]);this.verifyById();}
  private verifyById(){this.working.set(true);this.service.verify(this.publicId,this.studentId).subscribe({next:result=>{this.verified.set(result);this.currentIndex.set(0);this.working.set(false);},error:e=>{this.error.set(e.status===409?this.t('publicForm.alreadySubmitted'):this.t('publicForm.verifyError'));this.working.set(false);}});}
  currentSection():FormSection|null{return this.verified()?.form.definition.sections[this.currentIndex()]||null;}
  isChecked(id:string,option:string){return Array.isArray(this.answers[id])&&this.answers[id].includes(option);}
  toggleCheck(id:string,option:string,event:Event){const checked=(event.target as HTMLInputElement).checked;const values=Array.isArray(this.answers[id])?[...this.answers[id]]:[];this.answers[id]=checked?[...values,option]:values.filter((v:string)=>v!==option);}
  scale(q:FormQuestion){const min=q.min??1,max=q.max??5;return Array.from({length:Math.max(1,max-min+1)},(_,i)=>min+i);}
  grid(id:string):Record<string,any>{if(!this.answers[id]||typeof this.answers[id]!=='object')this.answers[id]={};return this.answers[id];}
  gridValue(id:string,row:string){return this.grid(id)[row];}
  setGridRadio(id:string,row:string,column:string){this.grid(id)[row]=column;}
  gridChecked(id:string,row:string,column:string){return Array.isArray(this.grid(id)[row])&&this.grid(id)[row].includes(column);}
  toggleGridCheck(id:string,row:string,column:string,event:Event){const checked=(event.target as HTMLInputElement).checked;const values=Array.isArray(this.grid(id)[row])?[...this.grid(id)[row]]:[];this.grid(id)[row]=checked?[...values,column]:values.filter((v:string)=>v!==column);}
  next(){this.error.set(null);const section=this.currentSection();if(!section)return;const invalid=this.validate(section);if(invalid){this.error.set(invalid);return;}let target:string|undefined;for(const q of section.questions.filter(item=>['multipleChoice','dropdown'].includes(item.type))){target=q.branches.find(b=>b.value===this.answers[q.id])?.targetSectionId||target;}if(target==='__submit'){this.submit();return;}const sections=this.verified()!.form.definition.sections;const nextIndex=target?sections.findIndex(s=>s.id===target):this.currentIndex()+1;if(nextIndex<0||nextIndex>=sections.length){this.submit();return;}this.sectionHistory.push(this.currentIndex());this.currentIndex.set(nextIndex);scrollTo({top:0,behavior:'smooth'});}
  back(){const index=this.sectionHistory.pop();if(index!=null)this.currentIndex.set(index);}
  isLastStep(){const section=this.currentSection();if(!section)return true;const sections=this.verified()!.form.definition.sections;let target:string|undefined;for(const q of section.questions)target=q.branches.find(b=>b.value===this.answers[q.id])?.targetSectionId||target;return target==='__submit'||(!target&&this.currentIndex()===sections.length-1);}
  validate(section:FormSection){for(const q of section.questions){const value=this.answers[q.id];const missing=value==null||value===''||(Array.isArray(value)&&!value.length)||(typeof value==='object'&&!Array.isArray(value)&&q.rows.some(row=>!value[row]||(Array.isArray(value[row])&&!value[row].length)));if(q.required&&missing)return `'${q.label}' ${this.t('publicForm.isRequired')}`;if(value&&q.validationPattern&&typeof value==='string'){try{if(!new RegExp(q.validationPattern).test(value))return q.validationMessage||`'${q.label}' ${this.t('publicForm.isInvalid')}`;}catch{}}}return null;}
  submit(){const result=this.verified()!;this.working.set(true);this.service.submit(this.publicId,{accessToken:result.accessToken,answers:this.answers}).subscribe({next:value=>{this.successMessage.set(value.message);this.submitted.set(true);this.working.set(false);scrollTo({top:0,behavior:'smooth'});},error:e=>{this.error.set(e.status===401?this.t('publicForm.sessionExpired'):e.status===409?this.t('publicForm.alreadySubmitted'):this.t('publicForm.submitError'));this.working.set(false);}});}
}
