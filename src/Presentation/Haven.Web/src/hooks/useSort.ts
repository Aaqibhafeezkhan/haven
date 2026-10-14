import { useCallback, useState } from 'react';

export interface SortState<K extends string> {
  sortBy: K;
  sortDescending: boolean;
}

/**
 * Column-sort state for tables. Clicking a new column sorts it ascending; clicking the active
 * column flips the direction. Pass `sortBy`, `sortDescending` and `onSort` to `TableHeader`,
 * and `sortBy`/`sortDescending` to the API query when sorting is done server-side.
 */
export function useSort<K extends string>(initialSortBy: K, initialDescending = false) {
  const [sort, setSort] = useState<SortState<K>>({
    sortBy: initialSortBy,
    sortDescending: initialDescending,
  });

  const onSort = useCallback((key: string) => {
    setSort(prev =>
      prev.sortBy === key
        ? { ...prev, sortDescending: !prev.sortDescending }
        : { sortBy: key as K, sortDescending: false }
    );
  }, []);

  return { ...sort, onSort };
}
