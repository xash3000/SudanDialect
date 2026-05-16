export interface AdminWordVisitStats {
  id: number;
  headword: string;
  visitCount: number;
  lastVisitedAt: string | null;
}

export interface AdminWordVisitPage {
  items: AdminWordVisitStats[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AdminWordVisitQuery {
  page?: number;
  pageSize?: number;
  sortBy?: 'visitCount' | 'headword' | 'lastVisitedAt';
  sortDirection?: 'asc' | 'desc';
}
