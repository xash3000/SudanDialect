import { Component, computed, inject } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { AdminAuthService } from '../../services/admin-auth.service';

@Component({
    selector: 'app-admin-settings-page',
    templateUrl: './admin-settings-page.component.html',
    styleUrl: './admin-settings-page.component.css'
})
export class AdminSettingsPageComponent {
    private readonly authService = inject(AdminAuthService);

    protected readonly username = computed(() => this.authService.session()?.username ?? '-');
    protected readonly apiBaseUrl = environment.apiBaseUrl;
}
