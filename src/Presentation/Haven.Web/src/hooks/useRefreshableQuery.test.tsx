import { act, renderHook, waitFor } from '@testing-library/react';
import { StrictMode } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { useRefreshableQuery } from './useRefreshableQuery';

describe('useRefreshableQuery', () => {
  it('finishes loading under StrictMode double-invoked effects', async () => {
    const fetcher = vi.fn().mockResolvedValue('data');
    const { result } = renderHook(() => useRefreshableQuery(fetcher, 'k', 0, 'failed'), {
      wrapper: StrictMode,
    });

    await waitFor(() => expect(result.current.data).toBe('data'));
    expect(result.current.loading).toBe(false);
  });

  it('finishes loading when a refresh tick supersedes an in-flight load', async () => {
    let resolveFirst: (v: string) => void = () => undefined;
    const fetcher = vi
      .fn()
      .mockImplementationOnce(() => new Promise<string>(r => (resolveFirst = r)))
      .mockResolvedValue('second');

    const { result, rerender } = renderHook(
      ({ tick }) => useRefreshableQuery(fetcher, 'k', tick, 'failed'),
      { initialProps: { tick: 0 } }
    );

    rerender({ tick: 1 });
    await waitFor(() => expect(result.current.data).toBe('second'));
    expect(result.current.loading).toBe(false);

    await act(async () => resolveFirst('stale'));
    expect(result.current.data).toBe('second');
  });

  it('keeps existing data when a silent refresh fails', async () => {
    const fetcher = vi.fn().mockResolvedValueOnce('data').mockRejectedValueOnce(new Error('boom'));
    const { result, rerender } = renderHook(
      ({ tick }) => useRefreshableQuery(fetcher, 'k', tick, 'failed'),
      { initialProps: { tick: 0 } }
    );

    await waitFor(() => expect(result.current.data).toBe('data'));
    rerender({ tick: 1 });
    await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));

    expect(result.current.error).toBeNull();
    expect(result.current.data).toBe('data');
  });
});
