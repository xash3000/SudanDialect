import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, switchMap, takeUntil, tap } from 'rxjs/operators';
import { SearchBarComponent } from '../../components/search-bar/search-bar.component';
import { WordCardComponent } from '../../components/word-card/word-card.component';
import { WordSearchResult } from '../../models/word-search-result';
import { WordSearchService } from '../../services/word-search.service';

@Component({
  selector: 'app-home-page',
  imports: [SearchBarComponent, WordCardComponent],
  templateUrl: './home-page.component.html',
  styleUrl: './home-page.component.css'
})
export class HomePageComponent implements OnDestroy {
  private readonly wordSearchService = inject(WordSearchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly searchInput$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  protected readonly searchQuery = signal('');
  protected readonly results = signal<WordSearchResult[]>([]);
  protected readonly selectedWord = signal<WordSearchResult | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isInputFocused = signal(false);
  protected readonly hasWordLoadError = signal(false);
  protected readonly showDropdown = computed(() => {
    return this.isInputFocused() && this.searchQuery().trim().length > 0;
  });
  protected readonly hasRequestError = signal(false);
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
            return of<WordSearchResult | null>(null);
          }

          const id = Number.parseInt(rawId, 10);
          if (!Number.isInteger(id) || id <= 0) {
            this.hasWordLoadError.set(true);
            this.selectedWord.set(null);
            return of<WordSearchResult | null>(null);
          }

          return this.wordSearchService.getById(id).pipe(
            catchError(() => {
              this.hasWordLoadError.set(true);
              this.selectedWord.set(null);
              return of<WordSearchResult | null>(null);
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
          this.results.set([word]);
        } else {
          this.results.set([]);
        }
      });
  }

  protected onSearchInput(value: string): void {
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
    this.isInputFocused.set(true);
  }

  protected onSearchInputBlur(): void {
    this.isInputFocused.set(false);
  }

  protected selectWord(word: WordSearchResult): void {
    this.selectedWord.set(word);
    this.isInputFocused.set(false);
    this.hasWordLoadError.set(false);
    this.searchQuery.set(word.headword);
    this.results.set([word]);
    void this.router.navigate(['/word', word.id], { replaceUrl: false });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.searchInput$.complete();
  }
}
