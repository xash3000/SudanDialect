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

  protected onEnter(event: Event): void {
    event.preventDefault();

    const input = this.searchInput?.nativeElement;
    if (!input) {
      return;
    }

    this.hideMobileKeyboardKeepingFocus(input);
  }

  protected selectWord(word: WordSearchResult, event: MouseEvent): void {
    event.preventDefault();
    this.searchInput?.nativeElement.blur();
    this.wordSelected.emit(word);
  }

  private hideMobileKeyboardKeepingFocus(input: HTMLInputElement): void {
    const navigatorWithVirtualKeyboard = navigator as Navigator & {
      virtualKeyboard?: {
        hide?: () => void;
      };
    };

    if (navigatorWithVirtualKeyboard.virtualKeyboard?.hide) {
      navigatorWithVirtualKeyboard.virtualKeyboard.hide();
      return;
    }

    const selectionStart = input.selectionStart ?? input.value.length;
    const selectionEnd = input.selectionEnd ?? input.value.length;
    const wasReadOnly = input.readOnly;

    // Fallback: toggle readOnly to dismiss virtual keyboard without forcing blur.
    input.readOnly = true;
    input.setSelectionRange(selectionStart, selectionEnd);

    window.setTimeout(() => {
      input.readOnly = wasReadOnly;
      input.setSelectionRange(selectionStart, selectionEnd);
    }, 0);
  }
}
