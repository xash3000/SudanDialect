import { DOCUMENT } from '@angular/common';
import { Component, NgZone, OnDestroy, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, switchMap, takeUntil, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { SearchBarComponent } from '../../components/search-bar/search-bar.component';
import { WordCardComponent } from '../../components/word-card/word-card.component';
import { Word } from '../../models/word';
import { WordSearchResult } from '../../models/word-search-result';
import { WordSearchService } from '../../services/word-search.service';

interface TurnstileApi {
  render(container: string | HTMLElement, options: Record<string, unknown>): string;
  reset(widgetId?: string): void;
  remove?(widgetId: string): void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

@Component({
  selector: 'app-home-page',
  imports: [SearchBarComponent, WordCardComponent],
  templateUrl: './home-page.component.html',
  styleUrl: './home-page.component.css'
})
export class HomePageComponent implements OnDestroy {
  private readonly document = inject(DOCUMENT);
  private readonly ngZone = inject(NgZone);
  private readonly wordSearchService = inject(WordSearchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly searchInput$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  protected readonly searchQuery = signal('');
  protected readonly results = signal<WordSearchResult[]>([]);
  protected readonly selectedWord = signal<Word | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isInputFocused = signal(false);
  protected readonly isMobileSearchActive = signal(false);
  protected readonly hasWordLoadError = signal(false);
  protected readonly showDropdown = computed(() => {
    const hasQuery = this.searchQuery().trim().length > 0;
    return (this.isInputFocused() || this.isMobileSearchActive()) && hasQuery;
  });
  protected readonly displayedResults = computed(() => {
    const allResults = this.results();
    return this.isMobileSearchActive() ? allResults.slice(0, 10) : allResults;
  });
  protected readonly hasRequestError = signal(false);
  protected readonly isSuggestionFormOpen = signal(false);
  protected readonly suggestionHeadword = signal('');
  protected readonly suggestionDefinition = signal('');
  protected readonly suggestionEmail = signal('');
  protected readonly suggestionError = signal('');
  protected readonly suggestionSuccess = signal('');
  protected readonly isSubmittingSuggestion = signal(false);
  protected readonly suggestionCaptchaContainerId = `suggestion-turnstile-${Math.random().toString(36).slice(2)}`;
  protected readonly isTurnstileConfigured = Boolean(environment.turnstileSiteKey);

  private turnstileLoadPromise: Promise<boolean> | null = null;
  private suggestionTurnstileWidgetId: string | null = null;
  private suggestionTurnstileToken: string | null = null;

  protected readonly shouldShowNoResults = computed(() => {
    return (
      !this.isLoading() &&
      !this.hasRequestError() &&
      this.searchQuery().trim().length > 0 &&
      this.results().length === 0
    );
  });

  constructor() {
    this.searchInput$
      .pipe(
        debounceTime(150),
        distinctUntilChanged(),
        tap(() => {
          this.isLoading.set(true);
          this.hasRequestError.set(false);
        }),
        switchMap((query) =>
          this.wordSearchService.search(query).pipe(
            catchError(() => {
              this.hasRequestError.set(true);
              return of<WordSearchResult[]>([]);
            })
          )
        ),
        takeUntil(this.destroy$)
      )
      .subscribe((results) => {
        this.results.set(results);
        this.isLoading.set(false);
      });

    this.route.paramMap
      .pipe(
        distinctUntilChanged((prev, curr) => {
          return prev.get('id') === curr.get('id');
        }),
        tap(() => {
          this.isLoading.set(true);
          this.hasWordLoadError.set(false);
        }),
        switchMap((params) => {
          const rawId = params.get('id');
          if (!rawId) {
            this.selectedWord.set(null);
            return of<Word | null>(null);
          }

          return this.wordSearchService.getById(rawId).pipe(
            catchError(() => {
              this.hasWordLoadError.set(true);
              this.selectedWord.set(null);
              return of<Word | null>(null);
            })
          );
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((word) => {
        this.selectedWord.set(word);
        this.isLoading.set(false);

        if (word) {
          this.searchQuery.set(word.headword);
        }

        this.results.set([]);
      });
  }

  protected onSearchInput(value: string): void {
    this.isMobileSearchActive.set(false);
    this.searchQuery.set(value);

    const trimmedQuery = value.trim();
    if (!trimmedQuery) {
      this.results.set([]);
      this.isLoading.set(false);
      this.hasRequestError.set(false);
      return;
    }

    this.searchInput$.next(trimmedQuery);
  }

  protected onSearchInputFocus(): void {
    this.isMobileSearchActive.set(false);
    this.isInputFocused.set(true);

    const trimmedQuery = this.searchQuery().trim();
    if (!trimmedQuery) {
      return;
    }

    this.searchInput$.next(trimmedQuery);
  }

  protected onSearchInputBlur(): void {
    this.isMobileSearchActive.set(false);
    this.isInputFocused.set(false);
  }

  protected onMobileSearchPressed(): void {
    this.isMobileSearchActive.set(true);
    this.isInputFocused.set(true);
  }

  protected selectWord(word: WordSearchResult): void {
    this.selectedWord.set(null);
    this.isLoading.set(true);
    this.isInputFocused.set(false);
    this.hasWordLoadError.set(false);
    this.searchQuery.set(word.headword);
    this.results.set([]);
    void this.router.navigate(['/word', word.id], { replaceUrl: false });
  }

  ngOnDestroy(): void {
    this.closeSuggestionForm();
    this.destroy$.next();
    this.destroy$.complete();
    this.searchInput$.complete();
  }

  protected openSuggestionForm(event: Event): void {
    event.preventDefault();
    this.resetSuggestionMessages();
    this.isSuggestionFormOpen.set(true);
    void this.initializeSuggestionTurnstileWidget();
  }

  protected closeSuggestionForm(): void {
    this.isSuggestionFormOpen.set(false);
    this.suggestionTurnstileToken = null;

    const turnstile = this.document.defaultView?.turnstile;
    if (turnstile && this.suggestionTurnstileWidgetId !== null) {
      if (turnstile.remove) {
        turnstile.remove(this.suggestionTurnstileWidgetId);
      } else {
        turnstile.reset(this.suggestionTurnstileWidgetId);
      }
    }

    this.suggestionTurnstileWidgetId = null;
  }

  protected onSuggestionHeadwordInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.suggestionHeadword.set(target.value);
  }

  protected onSuggestionDefinitionInput(event: Event): void {
    const target = event.target as HTMLTextAreaElement;
    this.suggestionDefinition.set(target.value);
  }

  protected onSuggestionEmailInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.suggestionEmail.set(target.value);
  }

  protected submitSuggestion(): void {
    this.suggestionError.set('');
    this.suggestionSuccess.set('');

    const normalizedHeadword = this.suggestionHeadword().trim();
    const normalizedDefinition = this.suggestionDefinition().trim();
    const normalizedEmail = this.suggestionEmail().trim();

    if (!normalizedHeadword) {
      this.suggestionError.set('يرجى إدخال الكلمة المقترحة.');
      return;
    }

    if (!normalizedDefinition) {
      this.suggestionError.set('يرجى إدخال شرح أو تعريف الكلمة.');
      return;
    }

    if (!this.suggestionTurnstileToken) {
      this.suggestionError.set('يرجى إكمال التحقق الأمني أولاً.');
      return;
    }

    this.isSubmittingSuggestion.set(true);

    this.wordSearchService
      .submitSuggestion(
        normalizedHeadword,
        normalizedDefinition,
        normalizedEmail ? normalizedEmail : null,
        this.suggestionTurnstileToken
      )
      .subscribe({
        next: () => {
          this.suggestionSuccess.set('تم إرسال الاقتراح بنجاح. شكراً لمساهمتك.');
          this.suggestionHeadword.set('');
          this.suggestionDefinition.set('');
          this.suggestionEmail.set('');
          this.resetSuggestionTurnstileWidget();
          this.isSubmittingSuggestion.set(false);
        },
        error: (error: unknown) => {
          this.suggestionError.set(this.extractSuggestionErrorMessage(error));
          this.resetSuggestionTurnstileWidget();
          this.isSubmittingSuggestion.set(false);
        }
      });
  }

  private resetSuggestionMessages(): void {
    this.suggestionError.set('');
    this.suggestionSuccess.set('');
  }

  private async initializeSuggestionTurnstileWidget(): Promise<void> {
    if (!this.isSuggestionFormOpen()) {
      return;
    }

    if (!environment.turnstileSiteKey) {
      this.suggestionError.set('خدمة التحقق غير مفعلة حالياً.');
      return;
    }

    const loaded = await this.ensureTurnstileLoaded();
    if (!loaded || !this.isSuggestionFormOpen()) {
      this.suggestionError.set('تعذر تحميل التحقق الأمني. حاول مرة أخرى.');
      return;
    }

    const turnstile = this.document.defaultView?.turnstile;
    const container = await this.waitForSuggestionCaptchaContainer();
    if (!turnstile || !container) {
      this.suggestionError.set('تعذر تحميل التحقق الأمني.');
      return;
    }

    if (this.suggestionTurnstileWidgetId !== null) {
      if (turnstile.remove) {
        turnstile.remove(this.suggestionTurnstileWidgetId);
      } else {
        turnstile.reset(this.suggestionTurnstileWidgetId);
      }
      this.suggestionTurnstileWidgetId = null;
    }

    container.innerHTML = '';

    this.suggestionTurnstileWidgetId = turnstile.render(container, {
      sitekey: environment.turnstileSiteKey,
      callback: (token: string) => {
        this.ngZone.run(() => {
          this.suggestionTurnstileToken = token;
          this.suggestionError.set('');
        });
      },
      'expired-callback': () => {
        this.ngZone.run(() => {
          this.suggestionTurnstileToken = null;
        });
      },
      'error-callback': () => {
        this.ngZone.run(() => {
          this.suggestionTurnstileToken = null;
          this.suggestionError.set('فشل التحقق الأمني. أعد المحاولة.');
        });
      }
    });
  }

  private ensureTurnstileLoaded(): Promise<boolean> {
    if (this.document.defaultView?.turnstile) {
      return Promise.resolve(true);
    }

    if (this.turnstileLoadPromise) {
      return this.turnstileLoadPromise;
    }

    this.turnstileLoadPromise = new Promise<boolean>((resolve) => {
      const scriptId = 'cloudflare-turnstile-script';
      const existingScript = this.document.getElementById(scriptId) as HTMLScriptElement | null;

      const resolveFromWindow = (): void => resolve(Boolean(this.document.defaultView?.turnstile));

      if (existingScript) {
        if (this.document.defaultView?.turnstile) {
          resolve(true);
          return;
        }

        existingScript.addEventListener('load', resolveFromWindow, { once: true });
        existingScript.addEventListener('error', () => resolve(false), { once: true });

        this.document.defaultView?.setTimeout(() => {
          resolve(Boolean(this.document.defaultView?.turnstile));
        }, 3000);

        return;
      }

      const script = this.document.createElement('script');
      script.id = scriptId;
      script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
      script.async = true;
      script.defer = true;
      script.onload = resolveFromWindow;
      script.onerror = () => resolve(false);
      this.document.head.appendChild(script);
    });

    return this.turnstileLoadPromise;
  }

  private resetSuggestionTurnstileWidget(): void {
    this.suggestionTurnstileToken = null;
    if (this.suggestionTurnstileWidgetId === null) {
      return;
    }

    this.document.defaultView?.turnstile?.reset(this.suggestionTurnstileWidgetId);
  }

  private extractSuggestionErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error?: { error?: string } }).error;
      if (payload && typeof payload.error === 'string' && payload.error.trim()) {
        return payload.error;
      }
    }

    return 'تعذر إرسال الاقتراح حالياً. حاول مرة أخرى.';
  }

  private async waitForSuggestionCaptchaContainer(maxAttempts = 12): Promise<HTMLElement | null> {
    for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
      const container = this.document.getElementById(this.suggestionCaptchaContainerId);
      if (container instanceof HTMLElement) {
        return container;
      }

      await this.delay(16);
    }

    return null;
  }

  private delay(milliseconds: number): Promise<void> {
    return new Promise((resolve) => {
      const defaultView = this.document.defaultView;
      if (defaultView) {
        defaultView.setTimeout(resolve, milliseconds);
        return;
      }

      resolve();
    });
  }
}
