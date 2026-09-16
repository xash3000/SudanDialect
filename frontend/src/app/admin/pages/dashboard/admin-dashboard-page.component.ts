import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { AdminDashboardMetrics, EmbeddingBackfillStatus } from '../../models/admin-word.model';
import { AdminWordService } from '../../services/admin-word.service';

@Component({
    selector: 'app-admin-dashboard-page',
    imports: [RouterLink],
    templateUrl: './admin-dashboard-page.component.html',
    styleUrl: './admin-dashboard-page.component.css'
})
export class AdminDashboardPageComponent {
    private readonly adminWordService = inject(AdminWordService);
    private readonly destroyRef = inject(DestroyRef);

    protected readonly metrics = signal<AdminDashboardMetrics | null>(null);
    protected readonly embeddingStatus = signal<EmbeddingBackfillStatus | null>(null);
    protected readonly isLoading = signal(true);
    protected readonly errorMessage = signal('');

    constructor() {
        this.loadMetrics();
    }

    protected refresh(): void {
        this.loadMetrics();
    }

    private loadMetrics(): void {
        this.isLoading.set(true);
        this.errorMessage.set('');

        forkJoin({
            metrics: this.adminWordService.getMetrics(),
            embeddingStatus: this.adminWordService.getEmbeddingStatus()
        })
            .pipe(
                takeUntilDestroyed(this.destroyRef),
                finalize(() => this.isLoading.set(false))
            )
            .subscribe({
                next: ({ metrics, embeddingStatus }) => {
                    this.metrics.set(metrics);
                    this.embeddingStatus.set(embeddingStatus);
                },
                error: () => {
                    this.metrics.set(null);
                    this.embeddingStatus.set(null);
                    this.errorMessage.set('تعذر تحميل مؤشرات لوحة التحكم. حاول مرة أخرى.');
                }
            });
    }
}
