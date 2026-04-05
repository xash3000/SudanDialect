import { WordSearchResult } from './word-search-result';

export type WordBrowsePage = {
    items: WordSearchResult[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
};
