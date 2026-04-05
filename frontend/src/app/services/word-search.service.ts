import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { WordBrowsePage } from '../models/word-browse-page';
import { WordSearchResult } from '../models/word-search-result';

@Injectable({ providedIn: 'root' })
export class WordSearchService {
  private readonly http = inject(HttpClient);

  getById(id: number): Observable<WordSearchResult> {
    return this.http.get<WordSearchResult>(`${environment.apiBaseUrl}/api/words/${id}`);
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
