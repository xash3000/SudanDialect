import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
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
  private readonly searchInput$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  protected readonly searchQuery = signal('');
  protected readonly results = signal<WordSearchResult[]>([]);
  protected readonly selectedWord = signal<WordSearchResult | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isInputFocused = signal(false);
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
  }

  protected onSearchInput(value: string): void {
    this.searchQuery.set(value);
    this.selectedWord.set(null);

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
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.searchInput$.complete();
  }
}
