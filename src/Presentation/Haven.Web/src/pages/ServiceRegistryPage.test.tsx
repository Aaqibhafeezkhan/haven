import { StrictMode } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { renderWithProviders, screen, waitFor } from '@/test/render';

import { ServiceRegistryPage } from './ServiceRegistryPage';

const calls = vi.hoisted(() => ({ getAll: 0, getSummary: 0, getDomains: 0, failSummary: false }));

vi.mock('@/api/serviceRegistry', () => ({
  serviceRegistryApi: {
    getAll: vi.fn(async () => {
      calls.getAll++;
      return {
        items: [
          {
            id: 'e1',
            serviceId: 's1',
            containerName: 'haven-web',
            serviceName: 'web',
            projectName: 'Acme',
            environmentName: 'prod',
            ports: [{ containerPort: 80, hostPort: 8080 }],
            status: 'Running',
            registeredAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
            serviceType: 'DockerImage',
            exposureMode: 'External',
            domains: [
              { id: 'd1', hostname: 'web.example.com', tlsMode: 'Acme', containerPort: 80 },
            ],
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 20,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      };
    }),
    getSummary: vi.fn(async () => {
      calls.getSummary++;
      if (calls.failSummary) throw new SyntaxError('Unexpected token <');
      return { total: 1, running: 1, stopped: 0, other: 0, domainCount: 1, exposedExternally: 1 };
    }),
    getDomains: vi.fn(async () => {
      calls.getDomains++;
      return {
        items: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 20,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
      };
    }),
  },
}));

vi.mock('@/api/projects', () => ({
  projectsApi: { getAll: vi.fn(async () => ({ items: [{ id: 'p1', name: 'Acme' }] })) },
}));
vi.mock('@/api/environments', () => ({
  environmentsApi: { getByProjectId: vi.fn(async () => []) },
}));

describe('ServiceRegistryPage', () => {
  it('renders entries without looping requests', async () => {
    renderWithProviders(<ServiceRegistryPage />);

    expect(await screen.findByText('haven-web')).toBeInTheDocument();
    expect(screen.getByText('Acme › prod · web')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /web\.example\.com/ })).toHaveAttribute(
      'href',
      'https://web.example.com'
    );

    await new Promise(r => setTimeout(r, 300));
    await waitFor(() => expect(calls.getAll).toBeLessThanOrEqual(2));
    expect(calls.getSummary).toBeLessThanOrEqual(2);
  });

  it('still renders entries under StrictMode when the summary endpoint fails', async () => {
    calls.failSummary = true;
    calls.getAll = 0;
    calls.getSummary = 0;
    renderWithProviders(
      <StrictMode>
        <ServiceRegistryPage />
      </StrictMode>
    );

    expect(await screen.findByText('haven-web')).toBeInTheDocument();
    await new Promise(r => setTimeout(r, 300));
    expect(calls.getAll).toBeLessThanOrEqual(4);
    expect(calls.getSummary).toBeLessThanOrEqual(4);
  });
});
