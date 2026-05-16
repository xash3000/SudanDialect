import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, finalize, of, startWith, switchMap, tap } from 'rxjs';
import { AdminWordVisitPage, AdminWordVisitQuery } from '../../models/admin-word-visit.model';
import { AdminWordService } from '../../services/admin-word.service';

@Component({
  selector: 'app-word-visits-page',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './word-visits-page.component.html',
  styleUrl: './word-visits-page.component.css'
})
export class WordVisitsPageComponent {
  private readonly adminWordService = inject(AdminWordService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly loadTrigger$ = new Subject<void>();

  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly sortBy = signal<'visitCount' | 'headword' | 'lastVisitedAt'>('visitCount');
  protected readonly sortDirection = signal<'asc' | 'desc'>('desc');

  protected readonly pageResponse = signal<AdminWordVisitPage>(this.createEmptyPage());
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly items = computed(() => this.pageResponse().items);
  protected readonly totalCount = computed(() => this.pageResponse().totalCount);
  protected readonly totalPages = computed(() => this.pageResponse().totalPages);
  protected readonly canGoPrevious = computed(() => this.page() > 1);
  protected readonly canGoNext = computed(() => this.totalPages() > 0 && this.page() < this.totalPages());

  constructor() {
    this.loadTrigger$
      .pipe(
        startWith(void 0),
        tap(() => {
          this.isLoading.set(true);
          this.errorMessage.set('');
        }),
        switchMap(() =>
          this.adminWordService.getWordVisits(this.buildQuery()).pipe(
            catchError(() => {
              this.errorMessage.set('تعذر تحميل إحصائيات الزيارات.');
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

  protected toggleSort(column: 'visitCount' | 'headword' | 'lastVisitedAt'): void {
    if (this.sortBy() === column) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(column);
      this.sortDirection.set(column === 'headword' ? 'asc' : 'desc');
    }

    this.page.set(1);
    this.loadTrigger$.next();
  }

  protected sortIndicator(column: 'visitCount' | 'headword' | 'lastVisitedAt'): string {
    if (this.sortBy() !== column) {
      return '↕';
    }

    return this.sortDirection() === 'asc' ? '↑' : '↓';
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

  private buildQuery(): AdminWordVisitQuery {
    return {
      page: this.page(),
      pageSize: this.pageSize(),
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection()
    };
  }

  private createEmptyPage(): AdminWordVisitPage {
    return {
      items: [],
      page: this.page(),
      pageSize: this.pageSize(),
      totalCount: 0,
      totalPages: 0
    };
  }
}
