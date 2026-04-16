import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, debounceTime, distinctUntilChanged, finalize, of, startWith, switchMap, tap } from 'rxjs';
import { AdminSearchBy, AdminSortBy, AdminWord, AdminWordTablePage, AdminWordTableQuery } from '../../models/admin-word.model';
import { AdminWordService } from '../../services/admin-word.service';
import { AdminToastService } from '../../services/admin-toast.service';

@Component({
  selector: 'app-word-list',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './word-list.component.html',
  styleUrl: './word-list.component.css'
})
export class WordListComponent {
  private readonly adminWordService = inject(AdminWordService);
  private readonly toastService = inject(AdminToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private readonly loadTrigger$ = new Subject<void>();

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly sortBy = signal<AdminSortBy>('updatedAt');
  protected readonly sortDirection = signal<'asc' | 'desc'>('desc');
  protected readonly searchBy = signal<AdminSearchBy>('headword');
  protected readonly activeFilter = signal<'all' | 'active' | 'inactive'>('all');

  protected readonly pageResponse = signal<AdminWordTablePage>(this.createEmptyPage());
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly words = computed(() => this.pageResponse().items);
  protected readonly totalCount = computed(() => this.pageResponse().totalCount);
  protected readonly totalPages = computed(() => this.pageResponse().totalPages);
  protected readonly canGoPrevious = computed(() => this.page() > 1);
  protected readonly canGoNext = computed(() => this.totalPages() > 0 && this.page() < this.totalPages());

  constructor() {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        const normalizedValue = this.normalizeSearchValue(value);
        if (normalizedValue !== value) {
          this.searchControl.setValue(normalizedValue, { emitEvent: false });
        }

        this.page.set(1);
        this.loadTrigger$.next();
      });

    this.loadTrigger$
      .pipe(
        startWith(void 0),
        tap(() => {
          this.isLoading.set(true);
          this.errorMessage.set('');
        }),
        switchMap(() =>
          this.adminWordService.getWords(this.buildQuery()).pipe(
            catchError(() => {
              this.errorMessage.set('تعذر تحميل قائمة الكلمات.');
              return of(this.createEmptyPage());
            }),
            finalize(() => this.isLoading.set(false))
          )
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((response) => {
        this.pageResponse.set(response);
        this.page.set(response.page);
      });
  }

  protected toggleSort(column: AdminSortBy): void {
    if (this.sortBy() === column) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(column);
      this.sortDirection.set(column === 'headword' ? 'asc' : 'desc');
    }

    this.page.set(1);
    this.loadTrigger$.next();
  }

  protected sortIndicator(column: AdminSortBy): string {
    if (this.sortBy() !== column) {
      return '↕';
    }

    return this.sortDirection() === 'asc' ? '↑' : '↓';
  }

  protected setActiveFilter(filter: 'all' | 'active' | 'inactive'): void {
    this.activeFilter.set(filter);
    this.page.set(1);
    this.loadTrigger$.next();
  }

  protected setSearchBy(value: string): void {
    const nextSearchBy: AdminSearchBy = value === 'id' ? 'id' : 'headword';

    if (this.searchBy() === nextSearchBy) {
      return;
    }

    this.searchBy.set(nextSearchBy);

    const normalizedValue = this.normalizeSearchValue(this.searchControl.value);
    if (normalizedValue !== this.searchControl.value) {
      this.searchControl.setValue(normalizedValue, { emitEvent: false });
    }

    this.page.set(1);
    this.loadTrigger$.next();
  }

  protected blockNonNumericSearchInput(event: KeyboardEvent): void {
    if (this.searchBy() !== 'id' || event.ctrlKey || event.metaKey || event.altKey) {
      return;
    }

    const allowedKeys = ['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'Tab', 'Home', 'End', 'Enter'];
    if (allowedKeys.includes(event.key)) {
      return;
    }

    if (!/^[0-9]$/.test(event.key)) {
      event.preventDefault();
    }
  }

  protected nextPage(): void {
    if (!this.canGoNext()) {
      return;
    }

    this.page.update((current) => current + 1);
    this.loadTrigger$.next();
  }

  protected previousPage(): void {
    if (!this.canGoPrevious()) {
      return;
    }

    this.page.update((current) => current - 1);
    this.loadTrigger$.next();
  }

  protected refresh(): void {
    this.loadTrigger$.next();
  }

  protected goToNewWord(): void {
    void this.router.navigate(['/admin/words/new']);
  }

  protected editWord(id: number): void {
    void this.router.navigate(['/admin/words', id, 'edit']);
  }

  protected deactivateWord(word: AdminWord): void {
    if (!word.isActive) {
      this.toastService.showError(`الكلمة [${word.id}] غير نشطة بالفعل.`);
      return;
    }

    const shouldDeactivate = window.confirm(`هل تريد تعطيل الكلمة ذات المعرّف [${word.id}]؟`);
    if (!shouldDeactivate) {
      return;
    }

    this.adminWordService
      .deactivateWord(word.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toastService.showSuccess(`تم تعطيل الكلمة [${word.id}] بنجاح.`);
          this.loadTrigger$.next();
        },
        error: () => {
          this.toastService.showError(`تعذر تعطيل الكلمة [${word.id}].`);
        }
      });
  }

  private buildQuery(): AdminWordTableQuery {
    const searchQuery = this.searchControl.value.trim();
    const activeFilter = this.activeFilter();

    return {
      page: this.page(),
      pageSize: this.pageSize(),
      query: searchQuery || undefined,
      searchBy: this.searchBy(),
      isActive: activeFilter === 'all' ? undefined : activeFilter === 'active',
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection()
    };
  }

  private createEmptyPage(): AdminWordTablePage {
    return {
      items: [],
      page: this.page(),
      pageSize: this.pageSize(),
      totalCount: 0,
      totalPages: 0
    };
  }

  private normalizeSearchValue(value: string): string {
    if (this.searchBy() !== 'id') {
      return value;
    }

    return value.replace(/[^0-9]/g, '');
  }
}
