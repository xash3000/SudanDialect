import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminAuthService } from '../services/admin-auth.service';
import { AdminToastService } from '../services/admin-toast.service';

@Component({
    selector: 'app-admin-layout',
    imports: [RouterOutlet, RouterLink, RouterLinkActive],
    templateUrl: './admin-layout.component.html',
    styleUrl: './admin-layout.component.css'
})
export class AdminLayoutComponent {
    private readonly authService = inject(AdminAuthService);
    private readonly toastService = inject(AdminToastService);

    protected readonly toasts = this.toastService.toasts;
    protected readonly username = computed(() => this.authService.session()?.username ?? 'المشرف');

    protected logout(): void {
        this.authService.logout();
    }

    protected dismissToast(toastId: number): void {
        this.toastService.dismiss(toastId);
    }
}
