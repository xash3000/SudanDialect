import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { AdminDashboardMetrics } from '../../models/admin-word.model';
import { AdminWordService } from '../../services/admin-word.service';

@Component({
    selector: 'app-admin-dashboard-page',
    templateUrl: './admin-dashboard-page.component.html',
    styleUrl: './admin-dashboard-page.component.css'
})
export class AdminDashboardPageComponent {
    private readonly adminWordService = inject(AdminWordService);
    private readonly destroyRef = inject(DestroyRef);

    protected readonly metrics = signal<AdminDashboardMetrics | null>(null);
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

        this.adminWordService
            .getMetrics()
            .pipe(
                takeUntilDestroyed(this.destroyRef),
                finalize(() => this.isLoading.set(false))
            )
            .subscribe({
                next: (response) => {
                    this.metrics.set(response);
                },
                error: () => {
                    this.metrics.set(null);
                    this.errorMessage.set('تعذر تحميل مؤشرات لوحة التحكم. حاول مرة أخرى.');
                }
            });
    }
}
