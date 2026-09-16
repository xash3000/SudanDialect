import { DOCUMENT } from '@angular/common';
import { Component, NgZone, OnDestroy, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, takeUntil } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { WordCardComponent } from '../../components/word-card/word-card.component';
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
  selector: 'app-semantic-search-page',
  imports: [WordCardComponent],
  templateUrl: './semantic-search-page.component.html',
  styleUrl: './semantic-search-page.component.css'
})
export class SemanticSearchPageComponent implements OnDestroy {
  private readonly document = inject(DOCUMENT);
  private readonly ngZone = inject(NgZone);
  private readonly wordSearchService = inject(WordSearchService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();

  protected readonly searchQuery = signal('');
  protected readonly results = signal<WordSearchResult[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly hasRequestError = signal(false);
  protected readonly hasSearched = signal(false);

  protected readonly isSuggestionFormOpen = signal(false);
  protected readonly suggestionHeadword = signal('');
  protected readonly suggestionDefinition = signal('');
  protected readonly suggestionEmail = signal('');
  protected readonly suggestionError = signal('');
  protected readonly suggestionSuccess = signal('');
  protected readonly isSubmittingSuggestion = signal(false);
  protected readonly suggestionCaptchaContainerId = `semantic-suggestion-turnstile-${Math.random().toString(36).slice(2)}`;
  protected readonly isTurnstileConfigured = Boolean(environment.turnstileSiteKey);

  private turnstileLoadPromise: Promise<boolean> | null = null;
  private suggestionTurnstileWidgetId: string | null = null;
  private suggestionTurnstileToken: string | null = null;

  protected readonly shouldShowNoResults = computed(() => {
    return (
      !this.isLoading() &&
      !this.hasRequestError() &&
      this.hasSearched() &&
      this.searchQuery().trim().length > 0 &&
      this.results().length === 0
    );
  });

  constructor() {
    this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe((params) => {
      const q = params['q'] ?? params['query'];
      if (q && typeof q === 'string' && q.trim()) {
        this.searchQuery.set(q.trim());
        this.executeSearch();
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchQuery.set(value);
  }

  protected executeSearch(): void {
    const query = this.searchQuery().trim();
    if (!query) {
      this.results.set([]);
      this.isLoading.set(false);
      this.hasRequestError.set(false);
      this.hasSearched.set(false);
      return;
    }

    this.isLoading.set(true);
    this.hasRequestError.set(false);
    this.hasSearched.set(true);

    this.wordSearchService
      .semanticSearch(query)
      .pipe(
        catchError(() => {
          this.hasRequestError.set(true);
          return of([]);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((results) => {
        this.results.set(results);
        this.isLoading.set(false);
      });
  }

  protected clearSearch(): void {
    this.searchQuery.set('');
    this.results.set([]);
    this.hasSearched.set(false);
    this.hasRequestError.set(false);
  }

  protected openSuggestionForm(event?: Event): void {
    if (event) {
      event.preventDefault();
    }

    this.suggestionHeadword.set('');
    this.suggestionDefinition.set(this.searchQuery().trim());
    this.suggestionEmail.set('');
    this.suggestionError.set('');
    this.suggestionSuccess.set('');
    this.isSuggestionFormOpen.set(true);
    this.suggestionTurnstileToken = null;

    setTimeout(() => {
      this.renderSuggestionTurnstile();
    }, 0);
  }

  protected closeSuggestionForm(): void {
    this.isSuggestionFormOpen.set(false);
    this.suggestionError.set('');
    this.suggestionSuccess.set('');
    this.suggestionTurnstileToken = null;

    if (this.suggestionTurnstileWidgetId && window.turnstile?.remove) {
      try {
        window.turnstile.remove(this.suggestionTurnstileWidgetId);
      } catch {
        // ignore
      }
      this.suggestionTurnstileWidgetId = null;
    }
  }

  protected onSuggestionHeadwordInput(event: Event): void {
    this.suggestionHeadword.set((event.target as HTMLInputElement).value);
  }

  protected onSuggestionDefinitionInput(event: Event): void {
    this.suggestionDefinition.set((event.target as HTMLTextAreaElement).value);
  }

  protected onSuggestionEmailInput(event: Event): void {
    this.suggestionEmail.set((event.target as HTMLInputElement).value);
  }

  protected submitSuggestion(): void {
    const headword = this.suggestionHeadword().trim();
    const definition = this.suggestionDefinition().trim();
    const email = this.suggestionEmail().trim() || null;

    if (!headword) {
      this.suggestionError.set('يرجى إدخال الكلمة المقترحة.');
      return;
    }

    if (!definition) {
      this.suggestionError.set('يرجى إدخال شرح أو تعريف الكلمة.');
      return;
    }

    if (this.isTurnstileConfigured && !this.suggestionTurnstileToken) {
      this.suggestionError.set('يرجى إكمال التحقق الأمني أولاً.');
      return;
    }

    this.isSubmittingSuggestion.set(true);
    this.suggestionError.set('');
    this.suggestionSuccess.set('');

    const token = this.suggestionTurnstileToken || 'turnstile-disabled';

    this.wordSearchService.submitSuggestion(headword, definition, email, token).subscribe({
      next: () => {
        this.isSubmittingSuggestion.set(false);
        this.suggestionSuccess.set('تم إرسال الاقتراح بنجاح. شكراً لمساهمتك.');
        this.suggestionHeadword.set('');
        this.suggestionDefinition.set('');
        this.suggestionEmail.set('');
        this.resetSuggestionTurnstile();
      },
      error: () => {
        this.isSubmittingSuggestion.set(false);
        this.suggestionError.set('تعذر إرسال الاقتراح حالياً. حاول مرة أخرى لاحقاً.');
        this.resetSuggestionTurnstile();
      }
    });
  }

  private renderSuggestionTurnstile(): void {
    if (!this.isTurnstileConfigured) {
      return;
    }

    this.ensureTurnstileLoaded().then((loaded) => {
      if (!loaded || !window.turnstile) {
        return;
      }

      const container = this.document.getElementById(this.suggestionCaptchaContainerId);
      if (!container) {
        return;
      }

      container.innerHTML = '';
      this.suggestionTurnstileWidgetId = window.turnstile.render(container, {
        sitekey: environment.turnstileSiteKey,
        theme: 'light',
        callback: (token: string) => {
          this.ngZone.run(() => {
            this.suggestionTurnstileToken = token;
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
          });
        }
      });
    });
  }

  private resetSuggestionTurnstile(): void {
    if (this.suggestionTurnstileWidgetId && window.turnstile) {
      window.turnstile.reset(this.suggestionTurnstileWidgetId);
      this.suggestionTurnstileToken = null;
    }
  }

  private ensureTurnstileLoaded(): Promise<boolean> {
    if (!this.isTurnstileConfigured) {
      return Promise.resolve(false);
    }

    if (window.turnstile) {
      return Promise.resolve(true);
    }

    if (this.turnstileLoadPromise) {
      return this.turnstileLoadPromise;
    }

    this.turnstileLoadPromise = new Promise<boolean>((resolve) => {
      const existingScript = this.document.querySelector<HTMLScriptElement>(
        'script[src^="https://challenges.cloudflare.com/turnstile/v0/api.js"]'
      );

      if (existingScript) {
        existingScript.addEventListener('load', () => resolve(true), { once: true });
        existingScript.addEventListener('error', () => resolve(false), { once: true });
        return;
      }

      const script = this.document.createElement('script');
      script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
      script.async = true;
      script.defer = true;
      script.onload = () => resolve(true);
      script.onerror = () => resolve(false);
      this.document.head.appendChild(script);
    });

    return this.turnstileLoadPromise;
  }
}
