import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { useSetUrlParams, useUrlState } from './useUrlState';

function wrapper({ children }: { children: ReactNode }) {
  return <MemoryRouter initialEntries={['/?foo=bar']}>{children}</MemoryRouter>;
}

describe('useUrlState', () => {
  it('reads the initial value from the URL', () => {
    const { result } = renderHook(() => useUrlState('foo', 'default'), { wrapper });
    expect(result.current[0]).toBe('bar');
  });

  it('falls back to the default value when the key is absent', () => {
    const { result } = renderHook(() => useUrlState('missing', 'default'), { wrapper });
    expect(result.current[0]).toBe('default');
  });

  it('updates the URL when set to a non-default value', () => {
    const { result } = renderHook(() => useUrlState('missing', 'default'), { wrapper });

    act(() => result.current[1]('newValue'));

    expect(result.current[0]).toBe('newValue');
  });

  it('removes the param when set back to the default value', () => {
    const { result } = renderHook(() => useUrlState('foo', 'default'), { wrapper });

    act(() => result.current[1]('default'));

    expect(result.current[0]).toBe('default');
  });

  it('removes the param when set to an empty string', () => {
    const { result } = renderHook(() => useUrlState('foo', 'default'), { wrapper });

    act(() => result.current[1](''));

    expect(result.current[0]).toBe('default');
  });
});

describe('useSetUrlParams', () => {
  it('applies several updates in one navigation instead of dropping all but the last', () => {
    const { result } = renderHook(
      () => ({
        set: useSetUrlParams(),
        a: useUrlState('a', ''),
        b: useUrlState('b', ''),
        c: useUrlState('c', ''),
      }),
      {
        wrapper: ({ children }: { children: ReactNode }) => (
          <MemoryRouter initialEntries={['/?a=1&b=2&c=3']}>{children}</MemoryRouter>
        ),
      }
    );

    act(() => result.current.set({ a: '', b: '', c: 'x' }));

    expect(result.current.a[0]).toBe('');
    expect(result.current.b[0]).toBe('');
    expect(result.current.c[0]).toBe('x');
  });

  it('shows the lost-update bug it replaces: sequential single setters keep only the last', () => {
    const { result } = renderHook(() => ({ a: useUrlState('a', ''), b: useUrlState('b', '') }), {
      wrapper: ({ children }: { children: ReactNode }) => (
        <MemoryRouter initialEntries={['/?a=1&b=2']}>{children}</MemoryRouter>
      ),
    });

    act(() => {
      result.current.a[1]('');
      result.current.b[1]('');
    });

    expect(result.current.b[0]).toBe('');
    expect(result.current.a[0]).toBe('1');
  });
});
