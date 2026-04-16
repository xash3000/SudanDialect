import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminCreateWordRequest,
  AdminDashboardMetrics,
  AdminUpdateWordRequest,
  AdminWord,
  AdminWordTablePage,
  AdminWordTableQuery
} from '../models/admin-word.model';

@Injectable({ providedIn: 'root' })
export class AdminWordService {
  private readonly http = inject(HttpClient);
  private readonly adminApiBaseUrl = `${environment.apiBaseUrl}/api/admin/words`;

  getMetrics(): Observable<AdminDashboardMetrics> {
    return this.http.get<AdminDashboardMetrics>(`${this.adminApiBaseUrl}/metrics`);
  }

  getWords(query: AdminWordTableQuery): Observable<AdminWordTablePage> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('sortDirection', query.sortDirection)
      .set('searchBy', query.searchBy);

    if (query.query?.trim()) {
      params = params.set('query', query.query.trim());
    }

    if (typeof query.isActive === 'boolean') {
      params = params.set('isActive', query.isActive);
    }

    return this.http.get<AdminWordTablePage>(this.adminApiBaseUrl, { params });
  }

  getWordById(id: number): Observable<AdminWord> {
    return this.http.get<AdminWord>(`${this.adminApiBaseUrl}/${id}`);
  }

  createWord(payload: AdminCreateWordRequest): Observable<AdminWord> {
    return this.http.post<AdminWord>(this.adminApiBaseUrl, payload);
  }

  updateWord(id: number, payload: AdminUpdateWordRequest): Observable<AdminWord> {
    return this.http.put<AdminWord>(`${this.adminApiBaseUrl}/${id}`, payload);
  }

  deactivateWord(id: number): Observable<{ id: number; deactivated: boolean }> {
    return this.http.delete<{ id: number; deactivated: boolean }>(`${this.adminApiBaseUrl}/${id}`);
  }
}
