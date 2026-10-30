export const BYTES_PER_MIB = 1024 * 1024;
export const NANO_CPUS_PER_CORE = 1_000_000_000;

/** Docker refuses to create a container with a memory limit below 6 MiB. */
export const MIN_MEMORY_LIMIT_MIB = 6;
/** Docker's minimum CPU quota is 1 million nano CPUs (0.001 cores). */
export const MIN_CPU_CORES = 0.001;

/** Resource limits as stored by the API: bytes and nano CPUs, where 0 means "not set". */
export interface ResourceLimits {
  memoryLimit: number;
  memorySwap: number;
  memoryReservation: number;
  nanoCpus: number;
}

/** Resource limits as typed by the user: memory in MiB, CPU in cores. Empty means "not set". */
export interface ResourceLimitsForm {
  memoryLimit: string;
  memorySwap: string;
  memoryReservation: string;
  cpus: string;
}

/** i18n keys (under `services:resourceLimits.errors`) describing why a field is invalid. */
export type ResourceLimitsErrorKey =
  | 'invalidNumber'
  | 'negative'
  | 'memoryMin'
  | 'swapInvalid'
  | 'swapRequiresLimit'
  | 'swapBelowLimit'
  | 'reservationAboveLimit'
  | 'cpuMin';

export type ResourceLimitsErrors = Partial<
  Record<keyof ResourceLimitsForm, ResourceLimitsErrorKey>
>;

export const EMPTY_RESOURCE_LIMITS_FORM: ResourceLimitsForm = {
  memoryLimit: '',
  memorySwap: '',
  memoryReservation: '',
  cpus: '',
};

const formatNumber = (value: number) => String(Number(value.toFixed(3)));

export function toResourceLimitsForm(limits?: Partial<ResourceLimits>): ResourceLimitsForm {
  const memory = (bytes?: number) =>
    bytes && bytes > 0 ? formatNumber(bytes / BYTES_PER_MIB) : '';

  return {
    memoryLimit: memory(limits?.memoryLimit),
    memorySwap: limits?.memorySwap === -1 ? '-1' : memory(limits?.memorySwap),
    memoryReservation: memory(limits?.memoryReservation),
    cpus:
      limits?.nanoCpus && limits.nanoCpus > 0
        ? formatNumber(limits.nanoCpus / NANO_CPUS_PER_CORE)
        : '',
  };
}

/** Parses a form field; empty is 0 ("not set"), anything unparseable is `null`. */
function parseField(raw: string): number | null {
  const trimmed = raw.trim();
  if (!trimmed) return 0;
  const value = Number(trimmed);
  return Number.isFinite(value) ? value : null;
}

export function parseResourceLimits(form: ResourceLimitsForm): {
  limits: ResourceLimits;
  errors: ResourceLimitsErrors;
} {
  const errors: ResourceLimitsErrors = {};

  const memoryLimit = parseField(form.memoryLimit);
  const memorySwap = parseField(form.memorySwap);
  const memoryReservation = parseField(form.memoryReservation);
  const cpus = parseField(form.cpus);

  if (memoryLimit === null) errors.memoryLimit = 'invalidNumber';
  else if (memoryLimit < 0) errors.memoryLimit = 'negative';
  else if (memoryLimit > 0 && memoryLimit < MIN_MEMORY_LIMIT_MIB) errors.memoryLimit = 'memoryMin';

  if (memoryReservation === null) errors.memoryReservation = 'invalidNumber';
  else if (memoryReservation < 0) errors.memoryReservation = 'negative';
  else if (memoryReservation > 0 && memoryLimit && memoryReservation > memoryLimit)
    errors.memoryReservation = 'reservationAboveLimit';

  if (memorySwap === null) errors.memorySwap = 'invalidNumber';
  else if (memorySwap < -1) errors.memorySwap = 'swapInvalid';
  else if (memorySwap > 0 && !memoryLimit) errors.memorySwap = 'swapRequiresLimit';
  else if (memorySwap > 0 && memoryLimit && memorySwap < memoryLimit)
    errors.memorySwap = 'swapBelowLimit';

  if (cpus === null) errors.cpus = 'invalidNumber';
  else if (cpus < 0) errors.cpus = 'negative';
  else if (cpus > 0 && cpus < MIN_CPU_CORES) errors.cpus = 'cpuMin';

  const toBytes = (mib: number | null) => Math.round((mib ?? 0) * BYTES_PER_MIB);

  return {
    limits: {
      memoryLimit: toBytes(memoryLimit),
      // -1 means "unlimited swap" to Docker and must not be scaled to bytes.
      memorySwap: memorySwap === -1 ? -1 : toBytes(memorySwap),
      memoryReservation: toBytes(memoryReservation),
      nanoCpus: Math.round((cpus ?? 0) * NANO_CPUS_PER_CORE),
    },
    errors,
  };
}

export const hasResourceLimitErrors = (errors: ResourceLimitsErrors) =>
  Object.keys(errors).length > 0;
