import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminAuthService } from '../../services/admin-auth.service';
import { AdminUserService } from '../../services/admin-user.service';
import { AdminWordService } from '../../services/admin-word.service';
import { AdminToastService } from '../../services/admin-toast.service';
import { AdminSettingsPageComponent } from './admin-settings-page.component';

describe('AdminSettingsPageComponent - Embeddings Backfill', () => {
  let authServiceMock: {
    session: ReturnType<typeof vi.fn>;
    roles: ReturnType<typeof vi.fn>;
  };
  let userServiceMock: {
    getUsers: ReturnType<typeof vi.fn>;
    createUser: ReturnType<typeof vi.fn>;
    updateUser: ReturnType<typeof vi.fn>;
    deleteUser: ReturnType<typeof vi.fn>;
  };
  let wordServiceMock: {
    getEmbeddingStatus: ReturnType<typeof vi.fn>;
    backfillEmbeddings: ReturnType<typeof vi.fn>;
  };
  let toastServiceMock: {
    showSuccess: ReturnType<typeof vi.fn>;
    showError: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    authServiceMock = {
      session: vi.fn().mockReturnValue({ username: 'admin_test' }),
      roles: vi.fn().mockReturnValue(['admin'])
    };

    userServiceMock = {
      getUsers: vi.fn().mockReturnValue(of([])),
      createUser: vi.fn().mockReturnValue(of({ id: '1', username: 'mod1', roles: ['moderator'] })),
      updateUser: vi.fn().mockReturnValue(of({ id: '1', username: 'mod1', roles: ['moderator'] })),
      deleteUser: vi.fn().mockReturnValue(of(void 0))
    };

    wordServiceMock = {
      getEmbeddingStatus: vi.fn().mockReturnValue(of({
        isEmbeddingServiceAvailable: true,
        dimensions: 256,
        totalWords: 100,
        wordsWithEmbeddings: 80,
        wordsWithoutEmbeddings: 20
      })),
      backfillEmbeddings: vi.fn().mockReturnValue(of({
        backfilledCount: 20,
        remainingMissing: 0,
        totalWords: 100,
        wordsWithEmbeddings: 100
      }))
    };

    toastServiceMock = {
      showSuccess: vi.fn(),
      showError: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [AdminSettingsPageComponent],
      providers: [
        { provide: AdminAuthService, useValue: authServiceMock },
        { provide: AdminUserService, useValue: userServiceMock },
        { provide: AdminWordService, useValue: wordServiceMock },
        { provide: AdminToastService, useValue: toastServiceMock }
      ]
    }).compileComponents();
  });

  it('should create component and load embedding status on initialization for admin', async () => {
    const fixture = TestBed.createComponent(AdminSettingsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
    expect(wordServiceMock.getEmbeddingStatus).toHaveBeenCalled();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('تضمينات الكلمات والبحث الذكي');
    expect(compiled.textContent).toContain('256');
    expect(compiled.textContent).toContain('نشط ومتوفر');
  });

  it('should trigger backfill when action button is clicked', async () => {
    const fixture = TestBed.createComponent(AdminSettingsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const backfillBtn = compiled.querySelector('.primary-btn') as HTMLButtonElement;
    expect(backfillBtn).toBeTruthy();

    backfillBtn.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(wordServiceMock.backfillEmbeddings).toHaveBeenCalledWith(50, false);
    expect(toastServiceMock.showSuccess).toHaveBeenCalledWith(expect.stringContaining('20'));
  });

  it('should pass forceAll true when checkbox is checked', async () => {
    const fixture = TestBed.createComponent(AdminSettingsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const forceAllCheckbox = compiled.querySelector('.control-group-checkbox input') as HTMLInputElement;
    expect(forceAllCheckbox).toBeTruthy();

    forceAllCheckbox.checked = true;
    forceAllCheckbox.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const backfillBtn = compiled.querySelector('.primary-btn') as HTMLButtonElement;
    backfillBtn.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(wordServiceMock.backfillEmbeddings).toHaveBeenCalledWith(50, true);
  });

  it('should handle backfill error gracefully', async () => {
    wordServiceMock.backfillEmbeddings.mockReturnValue(throwError(() => new Error('Backfill failed')));

    const fixture = TestBed.createComponent(AdminSettingsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const backfillBtn = compiled.querySelector('.primary-btn') as HTMLButtonElement;
    backfillBtn.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(toastServiceMock.showError).toHaveBeenCalled();
  });
});
