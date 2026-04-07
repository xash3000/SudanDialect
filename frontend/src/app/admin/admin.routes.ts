import { Routes } from '@angular/router';
import { adminAuthChildGuard, adminAuthGuard } from './guards/admin-auth.guard';
import { AdminLayoutComponent } from './layout/admin-layout.component';
import { AdminDashboardPageComponent } from './pages/dashboard/admin-dashboard-page.component';
import { AdminLoginPageComponent } from './pages/login/admin-login-page.component';
import { AdminSettingsPageComponent } from './pages/settings/admin-settings-page.component';
import { WordFormComponent } from './pages/words/word-form.component';
import { WordListComponent } from './pages/words/word-list.component';

export const adminRoutes: Routes = [
    {
        path: 'login',
        component: AdminLoginPageComponent
    },
    {
        path: '',
        component: AdminLayoutComponent,
        canActivate: [adminAuthGuard],
        canActivateChild: [adminAuthChildGuard],
        children: [
            {
                path: '',
                pathMatch: 'full',
                redirectTo: 'dashboard'
            },
            {
                path: 'dashboard',
                component: AdminDashboardPageComponent
            },
            {
                path: 'words',
                component: WordListComponent
            },
            {
                path: 'words/new',
                component: WordFormComponent
            },
            {
                path: 'words/:id/edit',
                component: WordFormComponent
            },
            {
                path: 'settings',
                component: AdminSettingsPageComponent
            }
        ]
    },
    {
        path: '**',
        redirectTo: 'dashboard'
    }
];
