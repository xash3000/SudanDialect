import { Component, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { WordCardComponent } from '../../components/word-card/word-card.component';
import { WordSearchResult } from '../../models/word-search-result';
import { WordSearchService } from '../../services/word-search.service';

@Component({
  selector: 'app-browse-page',
  imports: [WordCardComponent],
  templateUrl: './browse-page.component.html',
  styleUrl: './browse-page.component.css'
})
export class BrowsePageComponent {
  private readonly wordSearchService = inject(WordSearchService);
  private readonly pageSize = 80;

  protected readonly letters = [
    'ا', 'ب', 'ت', 'ث', 'ج', 'ح', 'خ', 'د', 'ذ', 'ر',
    'ز', 'س', 'ش', 'ص', 'ض', 'ط', 'ظ', 'ع', 'غ', 'ف',
    'ق', 'ك', 'ل', 'م', 'ن', 'ه', 'و', 'ي'
  ];

  protected readonly selectedLetter = signal<string | null>(null);
  protected readonly currentPage = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly words = signal<WordSearchResult[]>([]);
  protected readonly selectedWord = signal<WordSearchResult | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly hasError = signal(false);

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
    this.loadPage(1);
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) {
      return;
    }

    this.loadPage(page);
  }

  protected pageNumbers(): number[] {
    const total = this.totalPages();
    if (total <= 0) {
      return [];
    }

    return Array.from({ length: total }, (_, index) => index + 1);
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

  protected selectWord(word: WordSearchResult): void {
    this.selectedWord.set(word);
  }

  protected closeWordOverlay(): void {
    this.selectedWord.set(null);
  }
}
