import { describe, expect, it } from 'vitest';

import {
  EMPTY_RESOURCE_LIMITS_FORM,
  hasResourceLimitErrors,
  parseResourceLimits,
  toResourceLimitsForm,
} from './resourceLimits';

const form = (overrides: Partial<typeof EMPTY_RESOURCE_LIMITS_FORM>) => ({
  ...EMPTY_RESOURCE_LIMITS_FORM,
  ...overrides,
});

describe('toResourceLimitsForm', () => {
  it('leaves unset limits empty', () => {
    expect(toResourceLimitsForm()).toEqual(EMPTY_RESOURCE_LIMITS_FORM);
    expect(
      toResourceLimitsForm({ memoryLimit: 0, memorySwap: 0, memoryReservation: 0, nanoCpus: 0 })
    ).toEqual(EMPTY_RESOURCE_LIMITS_FORM);
  });

  it('converts bytes to MiB and nano CPUs to cores', () => {
    expect(
      toResourceLimitsForm({
        memoryLimit: 512 * 1024 * 1024,
        memorySwap: 1024 * 1024 * 1024,
        memoryReservation: 256 * 1024 * 1024,
        nanoCpus: 1_500_000_000,
      })
    ).toEqual({ memoryLimit: '512', memorySwap: '1024', memoryReservation: '256', cpus: '1.5' });
  });

  it('keeps -1 swap (unlimited) as -1', () => {
    expect(toResourceLimitsForm({ memorySwap: -1 }).memorySwap).toBe('-1');
  });
});

describe('parseResourceLimits', () => {
  it('treats an empty form as no limits and no errors', () => {
    const { limits, errors } = parseResourceLimits(EMPTY_RESOURCE_LIMITS_FORM);
    expect(limits).toEqual({ memoryLimit: 0, memorySwap: 0, memoryReservation: 0, nanoCpus: 0 });
    expect(hasResourceLimitErrors(errors)).toBe(false);
  });

  it('converts MiB to bytes and cores to nano CPUs', () => {
    const { limits, errors } = parseResourceLimits(
      form({ memoryLimit: '512', memorySwap: '1024', memoryReservation: '256', cpus: '0.5' })
    );
    expect(limits).toEqual({
      memoryLimit: 512 * 1024 * 1024,
      memorySwap: 1024 * 1024 * 1024,
      memoryReservation: 256 * 1024 * 1024,
      nanoCpus: 500_000_000,
    });
    expect(errors).toEqual({});
  });

  it('passes -1 swap through unscaled', () => {
    const { limits, errors } = parseResourceLimits(form({ memoryLimit: '512', memorySwap: '-1' }));
    expect(limits.memorySwap).toBe(-1);
    expect(errors).toEqual({});
  });

  it('accepts a swap equal to the memory limit (swap disabled)', () => {
    expect(parseResourceLimits(form({ memoryLimit: '512', memorySwap: '512' })).errors).toEqual({});
  });

  it('rejects non-numeric input', () => {
    const { errors } = parseResourceLimits(
      form({ memoryLimit: 'abc', memorySwap: '1x', memoryReservation: 'NaN', cpus: 'two' })
    );
    expect(errors).toEqual({
      memoryLimit: 'invalidNumber',
      memorySwap: 'invalidNumber',
      memoryReservation: 'invalidNumber',
      cpus: 'invalidNumber',
    });
  });

  it('rejects negative values, except -1 swap', () => {
    const { errors } = parseResourceLimits(
      form({ memoryLimit: '-5', memorySwap: '-2', memoryReservation: '-1', cpus: '-1' })
    );
    expect(errors).toEqual({
      memoryLimit: 'negative',
      memorySwap: 'swapInvalid',
      memoryReservation: 'negative',
      cpus: 'negative',
    });
  });

  it('enforces Docker minimums', () => {
    expect(parseResourceLimits(form({ memoryLimit: '5' })).errors.memoryLimit).toBe('memoryMin');
    expect(parseResourceLimits(form({ memoryLimit: '6' })).errors.memoryLimit).toBeUndefined();
    expect(parseResourceLimits(form({ cpus: '0.0001' })).errors.cpus).toBe('cpuMin');
    expect(parseResourceLimits(form({ cpus: '0.001' })).errors.cpus).toBeUndefined();
  });

  it('requires a memory limit when a swap limit is set', () => {
    expect(parseResourceLimits(form({ memorySwap: '1024' })).errors.memorySwap).toBe(
      'swapRequiresLimit'
    );
  });

  it('requires swap to be at least the memory limit', () => {
    expect(
      parseResourceLimits(form({ memoryLimit: '512', memorySwap: '256' })).errors.memorySwap
    ).toBe('swapBelowLimit');
  });

  it('requires the reservation to be at most the memory limit', () => {
    expect(
      parseResourceLimits(form({ memoryLimit: '512', memoryReservation: '1024' })).errors
        .memoryReservation
    ).toBe('reservationAboveLimit');
  });

  it('allows a reservation without a memory limit', () => {
    expect(parseResourceLimits(form({ memoryReservation: '256' })).errors).toEqual({});
  });
});
