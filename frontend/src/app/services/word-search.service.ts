import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { WordBrowsePage } from '../models/word-browse-page';
import { WordSearchResult } from '../models/word-search-result';
import { Word } from '../models/word';

@Injectable({ providedIn: 'root' })
export class WordSearchService {
  private readonly http = inject(HttpClient);

  getById(id: string): Observable<Word> {
    return this.http.get<Word>(`${environment.apiBaseUrl}/api/words/${id}`);
  }

  search(query: string): Observable<WordSearchResult[]> {
    const trimmedQuery = query.trim();
    if (!trimmedQuery) {
      return of([]);
    }

    const params = new HttpParams().set('query', trimmedQuery);
    return this.http
      .get<WordSearchResult[]>(`${environment.apiBaseUrl}/api/words/search`, { params })
      .pipe(map((results) => this.sortBySimilarity(results)));
  }

  semanticSearch(query: string): Observable<WordSearchResult[]> {
    const trimmedQuery = query.trim();
    if (!trimmedQuery) {
      return of([]);
    }

    const params = new HttpParams().set('query', trimmedQuery);
    return this.http
      .get<WordSearchResult[]>(`${environment.apiBaseUrl}/api/words/semantic-search`, { params })
      .pipe(map((results) => this.sortBySimilarity(results)));
  }

  browseByLetter(letter: string, page: number, pageSize: number): Observable<WordBrowsePage> {
    const trimmedLetter = letter.trim();
    if (!trimmedLetter) {
      return of({
        items: [],
        page: 1,
        pageSize,
        totalCount: 0,
        totalPages: 0
      });
    }

    const params = new HttpParams()
      .set('letter', trimmedLetter)
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http
      .get<WordBrowsePage>(`${environment.apiBaseUrl}/api/words/browse`, { params })
      .pipe(
        map((response) => ({
          ...response,
          items: [...response.items].sort((first, second) => first.headword.localeCompare(second.headword, 'ar'))
        }))
      );
  }

  submitFeedback(wordId: string, feedbackText: string, captchaToken: string): Observable<{ submitted: boolean }> {
    return this.http.post<{ submitted: boolean }>(
      `${environment.apiBaseUrl}/api/words/${wordId}/feedback`,
      {
        feedbackText,
        captchaToken
      }
    );
  }

  submitSuggestion(
    headword: string,
    definition: string,
    email: string | null,
    captchaToken: string
  ): Observable<{ submitted: boolean }> {
    return this.http.post<{ submitted: boolean }>(
      `${environment.apiBaseUrl}/api/words/suggestions`,
      {
        headword,
        definition,
        email,
        captchaToken
      }
    );
  }

  private sortBySimilarity(results: WordSearchResult[]): WordSearchResult[] {
    return [...results].sort((first, second) => {
      const scoreDifference = second.similarityScore - first.similarityScore;
      if (scoreDifference !== 0) {
        return scoreDifference;
      }

      return first.headword.localeCompare(second.headword, 'ar');
    });
  }
}
