export type AdminSortBy = 'id' | 'headword' | 'createdAt' | 'updatedAt' | 'isActive';
export type AdminSortDirection = 'asc' | 'desc';

export interface AdminDashboardMetrics {
    totalWords: number;
    activeWords: number;
    inactiveWords: number;
}

export interface AdminWord {
    id: number;
    headword: string;
    definition: string;
    isActive: boolean;
    createdAt: string;
    updatedAt: string;
}

export interface AdminWordTablePage {
    items: AdminWord[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

export interface AdminWordTableQuery {
    page: number;
    pageSize: number;
    query?: string;
    isActive?: boolean;
    sortBy: AdminSortBy;
    sortDirection: AdminSortDirection;
}

export interface AdminCreateWordRequest {
    headword: string;
    definition: string;
    isActive: boolean;
}

export interface AdminUpdateWordRequest {
    headword: string;
    definition: string;
    isActive: boolean;
}
