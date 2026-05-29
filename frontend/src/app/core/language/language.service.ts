import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';

type Language = 'en' | 'vi';
type Dictionary = Record<string, string>;

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly storageKey = 'olympia_student_360_language';
  private readonly dictionaries = new Map<Language, Dictionary>();
  private readonly dictionarySignal = signal<Dictionary>({});
  private readonly languageSignal = signal<Language>(this.getInitialLanguage());

  readonly language = this.languageSignal.asReadonly();

  constructor(private readonly http: HttpClient) {
    this.use(this.languageSignal());
  }

  use(language: Language) {
    localStorage.setItem(this.storageKey, language);
    this.languageSignal.set(language);

    const cachedDictionary = this.dictionaries.get(language);
    if (cachedDictionary) {
      this.dictionarySignal.set(cachedDictionary);
      return;
    }

    this.http.get<Dictionary>(`/assets/i18n/${language}.json`).subscribe({
      next: (dictionary) => {
        this.dictionaries.set(language, dictionary);
        if (this.languageSignal() === language) {
          this.dictionarySignal.set(dictionary);
        }
      },
      error: () => {
        this.dictionarySignal.set({});
      },
    });
  }

  toggle() {
    this.use(this.languageSignal() === 'en' ? 'vi' : 'en');
  }

  t(key: string): string {
    return this.dictionarySignal()[key] ?? key;
  }

  private getInitialLanguage(): Language {
    const storedLanguage = localStorage.getItem(this.storageKey);
    return storedLanguage === 'vi' ? 'vi' : 'en';
  }
}
