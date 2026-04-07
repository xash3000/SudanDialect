import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, take } from 'rxjs';
import { AdminAuthService } from '../../services/admin-auth.service';

@Component({
    selector: 'app-admin-login-page',
    imports: [ReactiveFormsModule],
    templateUrl: './admin-login-page.component.html',
    styleUrl: './admin-login-page.component.css'
})
export class AdminLoginPageComponent {
    private readonly formBuilder = inject(FormBuilder);
    private readonly authService = inject(AdminAuthService);
    private readonly router = inject(Router);
    private readonly route = inject(ActivatedRoute);

    protected readonly isSubmitting = signal(false);
    protected readonly errorMessage = signal('');

    protected readonly loginForm = this.formBuilder.nonNullable.group({
        username: ['', [Validators.required, Validators.minLength(3)]],
        password: ['', [Validators.required, Validators.minLength(8)]]
    });

    constructor() {
        this.authService
            .ensureAuthenticated()
            .pipe(take(1))
            .subscribe((isAuthenticated) => {
                if (isAuthenticated) {
                    void this.router.navigate(['/admin/dashboard']);
                }
            });
    }

    protected submit(): void {
        if (this.loginForm.invalid) {
            this.loginForm.markAllAsTouched();
            return;
        }

        this.isSubmitting.set(true);
        this.errorMessage.set('');

        const credentials = {
            username: this.loginForm.controls.username.value.trim(),
            password: this.loginForm.controls.password.value
        };

        this.authService
            .login(credentials)
            .pipe(finalize(() => this.isSubmitting.set(false)))
            .subscribe({
                next: () => {
                    const redirectTo = this.route.snapshot.queryParamMap.get('redirectTo');
                    if (redirectTo && redirectTo.startsWith('/admin')) {
                        void this.router.navigateByUrl(redirectTo);
                        return;
                    }

                    void this.router.navigate(['/admin/dashboard']);
                },
                error: () => {
                    this.errorMessage.set('فشل تسجيل الدخول. تحقق من اسم المستخدم وكلمة المرور.');
                }
            });
    }
}
