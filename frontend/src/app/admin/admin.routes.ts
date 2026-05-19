import { Routes } from '@angular/router';
import { adminAuthChildGuard, adminAuthGuard } from './guards/admin-auth.guard';
import { AdminLayoutComponent } from './layout/admin-layout.component';
import { AdminDashboardPageComponent } from './pages/dashboard/admin-dashboard-page.component';
import { AdminFeedbackPageComponent } from './pages/feedback/admin-feedback-page.component';
import { AdminAuditHistoryPageComponent } from './pages/history/admin-audit-history-page.component';
import { AdminLoginPageComponent } from './pages/login/admin-login-page.component';
import { AdminSettingsPageComponent } from './pages/settings/admin-settings-page.component';
import { AdminWordSuggestionsPageComponent } from './pages/word-suggestions/admin-word-suggestions-page.component';
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
                path: 'history',
                component: AdminAuditHistoryPageComponent
            },
            {
                path: 'feedback',
                component: AdminFeedbackPageComponent
            },
            {
                path: 'word-suggestions',
                component: AdminWordSuggestionsPageComponent
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
                path: 'words/:id/history',
                component: AdminAuditHistoryPageComponent
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
