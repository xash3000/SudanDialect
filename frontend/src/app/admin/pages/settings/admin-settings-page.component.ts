import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AdminAuthService } from '../../services/admin-auth.service';
import { AdminManagedUser } from '../../models/admin-user.model';
import { AdminUserService } from '../../services/admin-user.service';
import { AdminWordService } from '../../services/admin-word.service';
import { AdminToastService } from '../../services/admin-toast.service';
import { EmbeddingBackfillResult, EmbeddingBackfillStatus } from '../../models/admin-word.model';

interface ManagedUserViewModel extends AdminManagedUser {
  nextUsername: string;
  nextPassword: string;
  isSaving: boolean;
  errorMessage: string;
}

@Component({
  selector: 'app-admin-settings-page',
  imports: [ReactiveFormsModule],
  templateUrl: './admin-settings-page.component.html',
  styleUrl: './admin-settings-page.component.css'
})
export class AdminSettingsPageComponent {
  private readonly authService = inject(AdminAuthService);
  private readonly adminUserService = inject(AdminUserService);
  private readonly adminWordService = inject(AdminWordService);
  private readonly toastService = inject(AdminToastService);
  private readonly formBuilder = inject(FormBuilder);

  private readonly loadedInitialData = signal(false);

  protected readonly username = computed(() => this.authService.session()?.username ?? '-');
  protected readonly roles = computed(() => this.authService.roles());
  protected readonly isAdmin = computed(() => this.roles().includes('admin'));

  protected readonly users = signal<ManagedUserViewModel[]>([]);
  protected readonly isLoadingUsers = signal(false);
  protected readonly usersErrorMessage = signal('');

  protected readonly isCreatingUser = signal(false);
  protected readonly createErrorMessage = signal('');

  protected readonly embeddingStatus = signal<EmbeddingBackfillStatus | null>(null);
  protected readonly isLoadingEmbeddingStatus = signal(false);
  protected readonly embeddingStatusError = signal('');
  protected readonly isBackfilling = signal(false);
  protected readonly backfillResult = signal<EmbeddingBackfillResult | null>(null);
  protected readonly backfillError = signal('');
  protected readonly backfillBatchSize = signal(50);
  protected readonly backfillForceAll = signal(false);

  protected readonly embeddingProgressPercentage = computed(() => {
    const status = this.embeddingStatus();
    if (!status || status.totalWords === 0) {
      return 0;
    }
    return Math.round((status.wordsWithEmbeddings / status.totalWords) * 100);
  });

  protected readonly createUserForm = this.formBuilder.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3)]],
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  constructor() {
    effect(() => {
      if (!this.isAdmin() || this.loadedInitialData()) {
        return;
      }

      this.loadedInitialData.set(true);
      this.loadUsers();
      this.loadEmbeddingStatus();
    });
  }

  protected createUser(): void {
    if (!this.isAdmin()) {
      return;
    }

    if (this.createUserForm.invalid) {
      this.createUserForm.markAllAsTouched();
      return;
    }

    this.isCreatingUser.set(true);
    this.createErrorMessage.set('');

    const payload = {
      username: this.createUserForm.controls.username.value.trim(),
      password: this.createUserForm.controls.password.value
    };

    this.adminUserService
      .createUser(payload)
      .pipe(finalize(() => this.isCreatingUser.set(false)))
      .subscribe({
        next: (createdUser) => {
          this.createUserForm.reset({ username: '', password: '' });
          this.users.update((users) => this.sortUsers([...users, this.toViewModel(createdUser)]));
        },
        error: (error) => {
          this.createErrorMessage.set(this.extractApiError(error, 'تعذر إنشاء المستخدم.'));
        }
      });
  }

  protected updateUserDraftUsername(userId: string, username: string): void {
    this.updateUserViewModel(userId, (user) => ({
      ...user,
      nextUsername: username,
      errorMessage: ''
    }));
  }

  protected updateUserDraftPassword(userId: string, password: string): void {
    this.updateUserViewModel(userId, (user) => ({
      ...user,
      nextPassword: password,
      errorMessage: ''
    }));
  }

  protected saveUser(userId: string): void {
    if (!this.isAdmin()) {
      return;
    }

    const user = this.users().find((candidate) => candidate.id === userId);
    if (!user) {
      return;
    }

    const normalizedUsername = user.nextUsername.trim();
    if (normalizedUsername.length < 3) {
      this.updateUserViewModel(userId, (existingUser) => ({
        ...existingUser,
        errorMessage: 'اسم المستخدم يجب أن يكون 3 أحرف على الأقل.'
      }));
      return;
    }

    if (user.nextPassword.length < 8) {
      this.updateUserViewModel(userId, (existingUser) => ({
        ...existingUser,
        errorMessage: 'كلمة المرور يجب أن تكون 8 أحرف على الأقل.'
      }));
      return;
    }

    this.updateUserViewModel(userId, (existingUser) => ({
      ...existingUser,
      isSaving: true,
      errorMessage: ''
    }));

    this.adminUserService
      .updateUser(userId, {
        username: normalizedUsername,
        password: user.nextPassword
      })
      .pipe(
        finalize(() => {
          this.updateUserViewModel(userId, (existingUser) => ({
            ...existingUser,
            isSaving: false
          }));
        })
      )
      .subscribe({
        next: (updatedUser) => {
          this.users.update((users) =>
            this.sortUsers(
              users.map((existingUser) =>
                existingUser.id === userId
                  ? {
                    ...this.toViewModel(updatedUser),
                    nextPassword: ''
                  }
                  : existingUser
              )
            )
          );
        },
        error: (error) => {
          this.updateUserViewModel(userId, (existingUser) => ({
            ...existingUser,
            errorMessage: this.extractApiError(error, 'تعذر تحديث المستخدم.')
          }));
        }
      });
  }

  protected deleteUser(userId: string): void {
    if (!this.isAdmin()) {
      return;
    }

    const confirmed = window.confirm('هل أنت متأكد من حذف هذا المستخدم؟');
    if (!confirmed) {
      return;
    }

    this.usersErrorMessage.set('');

    this.adminUserService.deleteUser(userId).subscribe({
      next: () => {
        this.users.update((users) => users.filter((user) => user.id !== userId));
      },
      error: (error) => {
        this.usersErrorMessage.set(this.extractApiError(error, 'تعذر حذف المستخدم.'));
      }
    });
  }

  private loadUsers(): void {
    this.isLoadingUsers.set(true);
    this.usersErrorMessage.set('');

    this.adminUserService
      .getUsers()
      .pipe(finalize(() => this.isLoadingUsers.set(false)))
      .subscribe({
        next: (users) => {
          this.users.set(this.sortUsers(users.map((user) => this.toViewModel(user))));
        },
        error: (error) => {
          this.users.set([]);
          this.usersErrorMessage.set(this.extractApiError(error, 'تعذر تحميل قائمة المستخدمين.'));
        }
      });
  }

  private updateUserViewModel(userId: string, project: (user: ManagedUserViewModel) => ManagedUserViewModel): void {
    this.users.update((users) => users.map((user) => (user.id === userId ? project(user) : user)));
  }

  private toViewModel(user: AdminManagedUser): ManagedUserViewModel {
    return {
      ...user,
      nextUsername: user.username,
      nextPassword: '',
      isSaving: false,
      errorMessage: ''
    };
  }

  private sortUsers(users: ManagedUserViewModel[]): ManagedUserViewModel[] {
    return [...users].sort((left, right) => left.username.localeCompare(right.username));
  }

  protected loadEmbeddingStatus(): void {
    if (!this.isAdmin()) {
      return;
    }

    this.isLoadingEmbeddingStatus.set(true);
    this.embeddingStatusError.set('');

    this.adminWordService
      .getEmbeddingStatus()
      .pipe(finalize(() => this.isLoadingEmbeddingStatus.set(false)))
      .subscribe({
        next: (status) => {
          this.embeddingStatus.set(status);
        },
        error: (error) => {
          this.embeddingStatus.set(null);
          this.embeddingStatusError.set(this.extractApiError(error, 'تعذر تحميل حالة خدمة التضمين.'));
        }
      });
  }

  protected triggerBackfill(): void {
    if (!this.isAdmin()) {
      return;
    }

    this.isBackfilling.set(true);
    this.backfillError.set('');
    this.backfillResult.set(null);

    this.adminWordService
      .backfillEmbeddings(this.backfillBatchSize(), this.backfillForceAll())
      .pipe(finalize(() => this.isBackfilling.set(false)))
      .subscribe({
        next: (result) => {
          this.backfillResult.set(result);
          this.loadEmbeddingStatus();
          this.toastService.showSuccess(`تمت معالجة ${result.backfilledCount} كلمة بنجاح.`);
        },
        error: (error) => {
          const message = this.extractApiError(error, 'حدث خطأ أثناء معالجة التضمينات.');
          this.backfillError.set(message);
          this.toastService.showError(message);
        }
      });
  }

  protected updateBatchSize(value: string | number): void {
    const parsed = typeof value === 'number' ? value : parseInt(value, 10);
    if (!isNaN(parsed) && parsed > 0) {
      this.backfillBatchSize.set(Math.min(parsed, 500));
    }
  }

  protected toggleForceAll(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.backfillForceAll.set(target.checked);
  }

  private extractApiError(error: unknown, fallbackMessage: string): string {
    if (error instanceof HttpErrorResponse) {
      const payload = error.error;
      if (payload && typeof payload === 'object' && 'error' in payload) {
        const message = payload.error;
        if (typeof message === 'string' && message.trim().length > 0) {
          return message;
        }
      }
    }

    return fallbackMessage;
  }
}
