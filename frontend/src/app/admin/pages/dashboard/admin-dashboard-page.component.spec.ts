import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminWordService } from '../../services/admin-word.service';
import { AdminDashboardPageComponent } from './admin-dashboard-page.component';

describe('AdminDashboardPageComponent', () => {
  let wordServiceMock: {
    getMetrics: ReturnType<typeof vi.fn>;
    getEmbeddingStatus: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    wordServiceMock = {
      getMetrics: vi.fn().mockReturnValue(of({
        totalWords: 150,
        activeWords: 140,
        inactiveWords: 10
      })),
      getEmbeddingStatus: vi.fn().mockReturnValue(of({
        isEmbeddingServiceAvailable: true,
        dimensions: 256,
        totalWords: 150,
        wordsWithEmbeddings: 130,
        wordsWithoutEmbeddings: 20
      }))
    };

    await TestBed.configureTestingModule({
      imports: [AdminDashboardPageComponent],
      providers: [
        provideRouter([]),
        { provide: AdminWordService, useValue: wordServiceMock }
      ]
    }).compileComponents();
  });

  it('should create and render metrics and embedding status cards', async () => {
    const fixture = TestBed.createComponent(AdminDashboardPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('150');
    expect(compiled.textContent).toContain('140');
    expect(compiled.textContent).toContain('10');
    expect(compiled.textContent).toContain('التضمينات الدلالية');
    expect(compiled.textContent).toContain('130 / 150');
  });

  it('should handle load failure gracefully', async () => {
    wordServiceMock.getMetrics.mockReturnValue(throwError(() => new Error('Network error')));

    const fixture = TestBed.createComponent(AdminDashboardPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('تعذر تحميل مؤشرات لوحة التحكم');
  });
});
