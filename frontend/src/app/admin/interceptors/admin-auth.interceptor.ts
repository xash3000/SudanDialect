import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';

export const adminAuthInterceptor: HttpInterceptorFn = (request, next) => {
    const authService = inject(AdminAuthService);

    if (!request.url.includes('/api/admin/')) {
        return next(request);
    }

    const authenticatedRequest = request.withCredentials ? request : request.clone({ withCredentials: true });

    const isAuthEndpoint = authenticatedRequest.url.includes('/api/admin/auth/');
    const alreadyRetried = authenticatedRequest.headers.has('X-Auth-Retry');

    return next(authenticatedRequest).pipe(
        catchError((error: unknown) => {
            if (
                isAuthEndpoint
                || alreadyRetried
                || !(error instanceof HttpErrorResponse)
                || error.status !== 401
            ) {
                return throwError(() => error);
            }

            const retriedRequest = authenticatedRequest.clone({
                setHeaders: {
                    'X-Auth-Retry': '1'
                }
            });

            return authService.refreshSession().pipe(
                switchMap(() => next(retriedRequest)),
                catchError((refreshError) => {
                    authService.clearLocalSession();
                    return throwError(() => refreshError);
                })
            );
        })
    );
};
