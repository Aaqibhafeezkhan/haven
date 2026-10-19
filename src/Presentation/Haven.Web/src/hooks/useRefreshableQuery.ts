import { useEffect, useRef, useState } from 'react';

/**
 * Fetches `fetcher` whenever `queryKey` changes (showing `loading`) and again whenever
 * `refreshTick` changes (silently, so the current rows stay on screen while polling).
 */
export function useRefreshableQuery<T>(
  fetcher: () => Promise<T>,
  queryKey: string,
  refreshTick: number,
  fallbackError: string
) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetcherRef = useRef(fetcher);
  const lastKey = useRef<string | null>(null);
  const hasData = useRef(false);

  useEffect(() => {
    fetcherRef.current = fetcher;
  });

  useEffect(() => {
    let cancelled = false;
    const silent = lastKey.current === queryKey;
    lastKey.current = queryKey;

    const run = async () => {
      try {
        if (!silent) setLoading(true);
        const result = await fetcherRef.current();
        if (cancelled) return;
        hasData.current = true;
        setData(result);
        setError(null);
      } catch (err) {
        // A failed silent poll keeps the rows on screen, unless there's nothing to show yet.
        if (!cancelled && (!silent || !hasData.current)) {
          setError(err instanceof Error ? err.message : fallbackError);
        }
      } finally {
        // Always clear on the latest request: a poll can supersede a still-loading request
        // (or StrictMode can re-run the effect), and the superseded one never clears it.
        if (!cancelled) setLoading(false);
      }
    };
    void run();

    return () => {
      cancelled = true;
    };
  }, [queryKey, refreshTick, fallbackError]);

  return { data, loading, error };
}
