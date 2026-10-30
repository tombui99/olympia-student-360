import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { SchoolTranscript } from './transcripts.service';
import type { TranscriptPdfOptions } from './transcript-pdf';

@Injectable({ providedIn: 'root' })
export class StudentTranscriptPdfService {
  private readonly http = inject(HttpClient);
  private readonly assets = new Map<string, Promise<Uint8Array>>();

  async download(record: SchoolTranscript, options: TranscriptPdfOptions): Promise<void> {
    const { buildStudentTranscriptPdf, TRANSCRIPT_LETTERHEADS } = await import('./transcript-pdf');
    const font = options.format === 'bilingual' ? 'carlito/Carlito' : 'nunito-sans/NunitoSans';
    const [letterhead, regularFont, boldFont] = await Promise.all([
      this.asset(`/assets/letterhead/${encodeURIComponent(TRANSCRIPT_LETTERHEADS[options.format])}`),
      this.asset(`/assets/fonts/${font}-Regular.ttf`),
      this.asset(`/assets/fonts/${font}-Bold.ttf`),
    ]);
    const bytes = await buildStudentTranscriptPdf(record, options, { letterhead, regularFont, boldFont });
    const url = URL.createObjectURL(new Blob([new Uint8Array(bytes).buffer], { type: 'application/pdf' }));
    const link = document.createElement('a');
    const student = (record.sisUserId || 'student').replace(/[^\p{L}\p{N}_-]/gu, '-');
    link.href = url;
    link.download = `${student}-${options.format}-transcript.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 30_000);
  }

  private asset(url: string): Promise<Uint8Array> {
    let request = this.assets.get(url);
    if (!request) {
      request = firstValueFrom(this.http.get(url, { responseType: 'arraybuffer' }))
        .then((bytes) => new Uint8Array(bytes)).catch((error) => { this.assets.delete(url); throw error; });
      this.assets.set(url, request);
    }
    return request;
  }
}
