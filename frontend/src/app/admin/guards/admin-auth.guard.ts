import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { map } from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';

export const adminAuthGuard: CanActivateFn = (_route, state) => {
    const authService = inject(AdminAuthService);
    const router = inject(Router);

    if (authService.isAuthenticated()) {
        return true;
    }

    return authService.ensureAuthenticated().pipe(
        map((isAuthenticated) => {
            if (isAuthenticated) {
                return true;
            }

            return router.createUrlTree(['/admin/login'], {
                queryParams: {
                    redirectTo: state.url
                }
            });
        })
    );
};

export const adminAuthChildGuard: CanActivateChildFn = (route, state) => {
    return adminAuthGuard(route, state);
};
