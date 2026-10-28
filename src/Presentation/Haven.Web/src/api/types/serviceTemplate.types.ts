import { ExposureMode } from './service.types';

/** Memory and CPU limits in bytes and nano CPUs; 0 means "not set". */
export interface ResourceLimitsDto {
  memoryLimit: number;
  memorySwap: number;
  memoryReservation: number;
  nanoCpus: number;
}

export type TemplateInputFieldType = 'Text' | 'Select' | 'Secret';

export interface TemplateInputFieldDto {
  key: string;
  type: TemplateInputFieldType;
  label?: string;
  defaultValue?: string;
  immutable: boolean;
  options?: string[];
}

export interface ServiceTemplateSummaryDto {
  id: string;
  version: string;
  name: string;
  icon: string;
  category: string;
}

export interface ServiceTemplateDto extends ServiceTemplateSummaryDto {
  inputs: TemplateInputFieldDto[];
  /** The template's default resource limits, or null when it does not define any. */
  resourceLimits?: ResourceLimitsDto | null;
}

export interface CreateServiceFromTemplateInput {
  name?: string;
  alias?: string;
  inputValues: Record<string, string>;
  exposureMode: ExposureMode;
  ports: string[];
  /** Replaces the template's default resource limits. Omit to keep the template's defaults. */
  resourceLimits?: ResourceLimitsDto;
}
