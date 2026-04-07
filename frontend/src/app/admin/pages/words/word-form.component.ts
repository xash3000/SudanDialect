import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { distinctUntilChanged, finalize, map } from 'rxjs';
import { AdminCreateWordRequest, AdminUpdateWordRequest } from '../../models/admin-word.model';
import { AdminWordService } from '../../services/admin-word.service';
import { AdminToastService } from '../../services/admin-toast.service';

function containsArabicValidator(control: AbstractControl<string>): ValidationErrors | null {
    const value = control.value?.trim() ?? '';
    if (!value) {
        return null;
    }

    return /[\u0600-\u06FF]/.test(value) ? null : { arabicRequired: true };
}

@Component({
    selector: 'app-word-form',
    imports: [ReactiveFormsModule],
    templateUrl: './word-form.component.html',
    styleUrl: './word-form.component.css'
})
export class WordFormComponent {
    private readonly formBuilder = inject(FormBuilder);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly adminWordService = inject(AdminWordService);
    private readonly toastService = inject(AdminToastService);
    private readonly destroyRef = inject(DestroyRef);

    protected readonly isEditMode = signal(false);
    protected readonly wordId = signal<number | null>(null);
    protected readonly isLoading = signal(false);
    protected readonly isSaving = signal(false);
    protected readonly errorMessage = signal('');

    protected readonly pageTitle = computed(() => (this.isEditMode() ? 'تعديل كلمة' : 'إضافة كلمة جديدة'));

    protected readonly wordForm = this.formBuilder.nonNullable.group({
        headword: ['', [Validators.required, Validators.maxLength(200), containsArabicValidator]],
        definition: ['', [Validators.required, Validators.maxLength(4000), containsArabicValidator]],
        isActive: [true]
    });

    constructor() {
        this.route.paramMap
            .pipe(
                map((params) => params.get('id') ?? ''),
                distinctUntilChanged(),
                takeUntilDestroyed(this.destroyRef)
            )
            .subscribe((rawId) => {
                if (!rawId) {
                    this.resetForCreateMode();
                    return;
                }

                const id = Number.parseInt(rawId, 10);
                if (!Number.isInteger(id) || id <= 0) {
                    this.errorMessage.set('المعرف المطلوب في الرابط غير صالح.');
                    this.isEditMode.set(false);
                    this.wordId.set(null);
                    return;
                }

                this.isEditMode.set(true);
                this.wordId.set(id);
                this.loadWord(id);
            });
    }

    protected submit(): void {
        if (this.wordForm.invalid) {
            this.wordForm.markAllAsTouched();
            return;
        }

        this.isSaving.set(true);
        this.errorMessage.set('');

        const payloadBase = {
            headword: this.wordForm.controls.headword.value.trim(),
            definition: this.wordForm.controls.definition.value.trim(),
            isActive: this.wordForm.controls.isActive.value
        };

        if (this.isEditMode() && this.wordId()) {
            const payload: AdminUpdateWordRequest = payloadBase;

            this.adminWordService
                .updateWord(this.wordId()!, payload)
                .pipe(
                    takeUntilDestroyed(this.destroyRef),
                    finalize(() => this.isSaving.set(false))
                )
                .subscribe({
                    next: (word) => {
                        this.toastService.showSuccess(`تم حفظ الكلمة [${word.id}] بنجاح.`);
                        this.wordForm.patchValue({
                            headword: word.headword,
                            definition: word.definition,
                            isActive: word.isActive
                        });
                    },
                    error: (error: HttpErrorResponse) => {
                        this.handleSaveError(error, this.wordId());
                    }
                });

            return;
        }

        const payload: AdminCreateWordRequest = payloadBase;

        this.adminWordService
            .createWord(payload)
            .pipe(
                takeUntilDestroyed(this.destroyRef),
                finalize(() => this.isSaving.set(false))
            )
            .subscribe({
                next: (word) => {
                    this.toastService.showSuccess(`تم حفظ الكلمة [${word.id}] بنجاح.`);
                    void this.router.navigate(['/admin/words', word.id, 'edit']);
                },
                error: (error: HttpErrorResponse) => {
                    this.handleSaveError(error, null);
                }
            });
    }

    protected cancel(): void {
        void this.router.navigate(['/admin/words']);
    }

    protected fieldHasError(fieldName: 'headword' | 'definition', errorCode: string): boolean {
        const control = this.wordForm.controls[fieldName];
        return control.touched && control.hasError(errorCode);
    }

    private resetForCreateMode(): void {
        this.isEditMode.set(false);
        this.wordId.set(null);
        this.errorMessage.set('');
        this.wordForm.reset({
            headword: '',
            definition: '',
            isActive: true
        });
    }

    private loadWord(id: number): void {
        this.isLoading.set(true);
        this.errorMessage.set('');

        this.adminWordService
            .getWordById(id)
            .pipe(
                takeUntilDestroyed(this.destroyRef),
                finalize(() => this.isLoading.set(false))
            )
            .subscribe({
                next: (word) => {
                    this.wordForm.reset({
                        headword: word.headword,
                        definition: word.definition,
                        isActive: word.isActive
                    });
                },
                error: () => {
                    this.errorMessage.set(`تعذر تحميل الكلمة [${id}]`);
                }
            });
    }

    private handleSaveError(error: HttpErrorResponse, wordId: number | null): void {
        const backendMessage = typeof error.error?.error === 'string' ? error.error.error : '';
        const fallbackMessage = wordId
            ? `تعذر حفظ التعديلات على الكلمة [${wordId}].`
            : 'تعذر إنشاء الكلمة الجديدة.';

        this.errorMessage.set(backendMessage || fallbackMessage);
        this.toastService.showError(this.errorMessage());
    }
}
