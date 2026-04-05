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

  protected readonly letters = [
    'ا', 'ب', 'ت', 'ث', 'ج', 'ح', 'خ', 'د', 'ذ', 'ر',
    'ز', 'س', 'ش', 'ص', 'ض', 'ط', 'ظ', 'ع', 'غ', 'ف',
    'ق', 'ك', 'ل', 'م', 'ن', 'ه', 'و', 'ي'
  ];

  protected readonly selectedLetter = signal<string | null>(null);
  protected readonly words = signal<WordSearchResult[]>([]);
  protected readonly selectedWord = signal<WordSearchResult | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly hasError = signal(false);

  protected selectLetter(letter: string): void {
    if (this.selectedLetter() === letter) {
      return;
    }

    this.selectedLetter.set(letter);
    this.selectedWord.set(null);
    this.isLoading.set(true);
    this.hasError.set(false);

    this.wordSearchService
      .browseByLetter(letter)
      .pipe(
        catchError(() => {
          this.hasError.set(true);
          return of<WordSearchResult[]>([]);
        })
      )
      .subscribe((results) => {
        this.words.set(results);
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
