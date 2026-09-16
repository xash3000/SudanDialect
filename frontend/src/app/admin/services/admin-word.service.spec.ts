import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { AdminWordService } from './admin-word.service';

describe('AdminWordService', () => {
  let service: AdminWordService;
  let httpTesting: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/api/admin/words`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AdminWordService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminWordService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should get metrics', () => {
    const mockMetrics = { totalWords: 100, activeWords: 90, inactiveWords: 10 };

    service.getMetrics().subscribe((metrics) => {
      expect(metrics).toEqual(mockMetrics);
    });

    const req = httpTesting.expectOne(`${baseUrl}/metrics`);
    expect(req.request.method).toBe('GET');
    req.flush(mockMetrics);
  });

  it('should get embedding status', () => {
    const mockStatus = {
      isEmbeddingServiceAvailable: true,
      dimensions: 256,
      totalWords: 100,
      wordsWithEmbeddings: 80,
      wordsWithoutEmbeddings: 20
    };

    service.getEmbeddingStatus().subscribe((status) => {
      expect(status).toEqual(mockStatus);
    });

    const req = httpTesting.expectOne(`${baseUrl}/embedding-status`);
    expect(req.request.method).toBe('GET');
    req.flush(mockStatus);
  });

  it('should call backfill embeddings with query params', () => {
    const mockResult = {
      backfilledCount: 20,
      remainingMissing: 0,
      totalWords: 100,
      wordsWithEmbeddings: 100
    };

    service.backfillEmbeddings(100, true).subscribe((result) => {
      expect(result).toEqual(mockResult);
    });

    const req = httpTesting.expectOne((r) =>
      r.url === `${baseUrl}/backfill-embeddings` &&
      r.params.get('batchSize') === '100' &&
      r.params.get('forceAll') === 'true'
    );
    expect(req.request.method).toBe('POST');
    req.flush(mockResult);
  });
});
