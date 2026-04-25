import { Component, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { WordCardComponent } from '../../components/word-card/word-card.component';
import { Word } from '../../models/word';
import { WordSummary } from '../../models/word-summary';
import { WordSearchService } from '../../services/word-search.service';

@Component({
  selector: 'app-browse-page',
  imports: [WordCardComponent],
  templateUrl: './browse-page.component.html',
  styleUrl: './browse-page.component.css'
})
export class BrowsePageComponent {
  private readonly wordSearchService = inject(WordSearchService);
  private readonly pageSize = 40;

  protected readonly letters = [
    'ا', 'ب', 'ت', 'ث', 'ج', 'ح', 'خ', 'د', 'ذ', 'ر',
    'ز', 'س', 'ش', 'ص', 'ض', 'ط', 'ظ', 'ع', 'غ', 'ف',
    'ق', 'ك', 'ل', 'م', 'ن', 'ه', 'و', 'ي'
  ];

  protected readonly selectedLetter = signal<string | null>(null);
  protected readonly currentPage = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly words = signal<WordSummary[]>([]);
  protected readonly selectedWord = signal<Word | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isWordLoading = signal(false);
  protected readonly hasError = signal(false);
  protected readonly hasWordLoadError = signal(false);

  protected selectLetter(letter: string): void {
    if (this.selectedLetter() === letter) {
      return;
    }

    this.selectedLetter.set(letter);
    this.currentPage.set(1);
    this.selectedWord.set(null);
    this.words.set([]);
    this.totalPages.set(0);
    this.totalCount.set(0);
    this.isWordLoading.set(false);
    this.hasWordLoadError.set(false);
    this.loadPage(1);
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) {
      return;
    }

    this.loadPage(page);
  }

  protected previousPage(): void {
    this.goToPage(this.currentPage() - 1);
  }

  protected nextPage(): void {
    this.goToPage(this.currentPage() + 1);
  }

  protected firstPage(): void {
    this.goToPage(1);
  }

  protected lastPage(): void {
    this.goToPage(this.totalPages());
  }

  protected canGoPrevious(): boolean {
    return this.currentPage() > 1;
  }

  protected canGoNext(): boolean {
    return this.totalPages() > 0 && this.currentPage() < this.totalPages();
  }

  protected visiblePages(): number[] {
    const total = this.totalPages();
    if (total <= 0) {
      return [];
    }

    const current = this.currentPage();
    const radius = 5;
    const desiredWindowSize = radius * 2 + 1;

    let start = Math.max(1, current - radius);
    let end = Math.min(total, current + radius);

    if (end - start + 1 < desiredWindowSize) {
      if (start === 1) {
        end = Math.min(total, start + desiredWindowSize - 1);
      } else if (end === total) {
        start = Math.max(1, end - desiredWindowSize + 1);
      }
    }

    return Array.from({ length: end - start + 1 }, (_, index) => start + index);
  }

  private loadPage(page: number): void {
    const letter = this.selectedLetter();
    if (!letter) {
      return;
    }

    this.isLoading.set(true);
    this.hasError.set(false);

    this.wordSearchService
      .browseByLetter(letter, page, this.pageSize)
      .pipe(
        catchError(() => {
          this.hasError.set(true);
          return of({
            items: [],
            page,
            pageSize: this.pageSize,
            totalCount: 0,
            totalPages: 0
          });
        })
      )
      .subscribe((response) => {
        this.words.set(response.items);
        this.currentPage.set(response.page);
        this.totalPages.set(response.totalPages);
        this.totalCount.set(response.totalCount);
        this.isLoading.set(false);
      });
  }

  protected selectWord(word: WordSummary): void {
    this.isWordLoading.set(true);
    this.hasWordLoadError.set(false);

    this.wordSearchService
      .getById(word.id)
      .pipe(
        catchError(() => {
          this.hasWordLoadError.set(true);
          return of<Word | null>(null);
        })
      )
      .subscribe((loadedWord) => {
        this.selectedWord.set(loadedWord);
        this.isWordLoading.set(false);
      });
  }

  protected closeWordOverlay(): void {
    this.selectedWord.set(null);
  }
}
