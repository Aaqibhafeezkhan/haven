import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';

/**
 * Sets several URL params in a single navigation. Calling multiple `useUrlState` setters in a row
 * doesn't work: each one rebuilds the query string from the same render-time params, so only the
 * last write survives. Empty-string values remove the param.
 */
export function useSetUrlParams(): (updates: Record<string, string>) => void {
  const [, setSearchParams] = useSearchParams();

  return useCallback(
    (updates: Record<string, string>) => {
      setSearchParams(
        prev => {
          const next = new URLSearchParams(prev);
          for (const [key, value] of Object.entries(updates)) {
            if (value === '') next.delete(key);
            else next.set(key, value);
          }
          return next;
        },
        { replace: true }
      );
    },
    [setSearchParams]
  );
}

export function useUrlState(key: string, defaultValue: string): [string, (value: string) => void] {
  const [searchParams, setSearchParams] = useSearchParams();
  const value = searchParams.get(key) ?? defaultValue;

  const setValue = useCallback(
    (newValue: string) => {
      setSearchParams(
        prev => {
          const next = new URLSearchParams(prev);
          if (newValue === '' || newValue === defaultValue) {
            next.delete(key);
          } else {
            next.set(key, newValue);
          }
          return next;
        },
        { replace: true }
      );
    },
    [key, defaultValue, setSearchParams]
  );

  return [value, setValue];
}
