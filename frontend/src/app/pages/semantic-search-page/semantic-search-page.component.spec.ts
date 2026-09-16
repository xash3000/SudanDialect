import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { WordSearchService } from '../../services/word-search.service';
import { SemanticSearchPageComponent } from './semantic-search-page.component';

describe('SemanticSearchPageComponent', () => {
  let wordSearchServiceMock: {
    search: ReturnType<typeof vi.fn>;
    semanticSearch: ReturnType<typeof vi.fn>;
    getById: ReturnType<typeof vi.fn>;
    submitSuggestion: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    wordSearchServiceMock = {
      search: vi.fn().mockReturnValue(of([])),
      semanticSearch: vi.fn().mockReturnValue(of([])),
      getById: vi.fn().mockReturnValue(of({
        id: '1',
        headword: 'جبنة',
        definition: 'قهوة سودانية تقليدية'
      })),
      submitSuggestion: vi.fn().mockReturnValue(of({ submitted: true }))
    };

    await TestBed.configureTestingModule({
      imports: [SemanticSearchPageComponent],
      providers: [
        provideRouter([]),
        { provide: WordSearchService, useValue: wordSearchServiceMock }
      ]
    }).compileComponents();
  });

  it('should create the semantic search page component', () => {
    const fixture = TestBed.createComponent(SemanticSearchPageComponent);
    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
  });

  it('should not search on typing input alone', async () => {
    const fixture = TestBed.createComponent(SemanticSearchPageComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('.semantic-input') as HTMLInputElement;
    input.value = 'حاجة سخنة';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    // Wait a bit to ensure no auto-search fires
    await new Promise((resolve) => setTimeout(resolve, 400));
    expect(wordSearchServiceMock.semanticSearch).not.toHaveBeenCalled();
  });

  it('should trigger search when search button is clicked', async () => {
    const mockResults = [
      { id: 'w1', headword: 'جبنة', definition: 'القهوة بالبهارات', similarityScore: 0.88 }
    ];
    wordSearchServiceMock.semanticSearch.mockReturnValue(of(mockResults));

    const fixture = TestBed.createComponent(SemanticSearchPageComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('.semantic-input') as HTMLInputElement;
    input.value = 'حاجة سخنة';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const searchBtn = compiled.querySelector('.search-btn') as HTMLButtonElement;
    searchBtn.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(wordSearchServiceMock.semanticSearch).toHaveBeenCalledWith('حاجة سخنة');
  });

  it('should trigger search when Enter key is pressed in search input', async () => {
    const mockResults = [
      { id: 'w1', headword: 'جبنة', definition: 'القهوة بالبهارات', similarityScore: 0.88 }
    ];
    wordSearchServiceMock.semanticSearch.mockReturnValue(of(mockResults));

    const fixture = TestBed.createComponent(SemanticSearchPageComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('.semantic-input') as HTMLInputElement;
    input.value = 'شخص كريم';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(wordSearchServiceMock.semanticSearch).toHaveBeenCalledWith('شخص كريم');
  });

  it('should not render similarity percentage badge on search results', async () => {
    const mockResults = [
      { id: 'w1', headword: 'جبنة', definition: 'القهوة بالبهارات', similarityScore: 0.88 }
    ];
    wordSearchServiceMock.semanticSearch.mockReturnValue(of(mockResults));

    const fixture = TestBed.createComponent(SemanticSearchPageComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('.semantic-input') as HTMLInputElement;
    input.value = 'جبنة';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const searchBtn = compiled.querySelector('.search-btn') as HTMLButtonElement;
    searchBtn.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const badge = compiled.querySelector('.similarity-badge');
    expect(badge).toBeNull();
  });
});
