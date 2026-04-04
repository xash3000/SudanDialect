import { Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { WordSearchResult } from '../../models/word-search-result';

@Component({
  selector: 'app-search-bar',
  templateUrl: './search-bar.component.html',
  styleUrl: './search-bar.component.css'
})
export class SearchBarComponent {
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  @Input() query = '';
  @Input() results: WordSearchResult[] = [];
  @Input() isLoading = false;
  @Input() showDropdown = false;
  @Input() hasRequestError = false;
  @Input() shouldShowNoResults = false;

  @Output() queryChanged = new EventEmitter<string>();
  @Output() wordSelected = new EventEmitter<WordSearchResult>();
  @Output() inputFocused = new EventEmitter<void>();
  @Output() inputBlurred = new EventEmitter<void>();

  protected onInput(value: string): void {
    this.queryChanged.emit(value);
  }

  protected onFocus(): void {
    this.inputFocused.emit();
  }

  protected onBlur(): void {
    this.inputBlurred.emit();
  }

  protected selectWord(word: WordSearchResult, event: MouseEvent): void {
    event.preventDefault();
    this.searchInput?.nativeElement.blur();
    this.wordSelected.emit(word);
  }
}
