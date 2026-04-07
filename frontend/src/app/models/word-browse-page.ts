import { Word } from './word';

export type WordBrowsePage = {
    items: Word[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
};
