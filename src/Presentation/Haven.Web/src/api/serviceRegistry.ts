import { apiClient, Params } from './client';
import { PagedResult } from './types';
import {
  GetServiceRegistryDomainsParams,
  GetServiceRegistryParams,
  ServiceRegistryDomainListItemDto,
  ServiceRegistryEntryDto,
  ServiceRegistrySummaryDto,
} from './types/service.types';

export const serviceRegistryApi = {
  getAll: (params?: GetServiceRegistryParams) =>
    apiClient.get<PagedResult<ServiceRegistryEntryDto>>(
      '/service-registry',
      params as Params | undefined
    ),

  getSummary: () => apiClient.get<ServiceRegistrySummaryDto>('/service-registry/summary'),

  getDomains: (params?: GetServiceRegistryDomainsParams) =>
    apiClient.get<PagedResult<ServiceRegistryDomainListItemDto>>(
      '/service-registry/domains',
      params as Params | undefined
    ),
};
